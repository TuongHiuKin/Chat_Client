using System.Security.Cryptography;
using System.Text.Json;
using ChatServer.Data;
using ChatServer.Models;
using ChatServer.Protocol;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services;

// Requests on a connection are ordered. Different clients run concurrently.
public sealed class FileService : IDisposable
{
    public const long MaxFileSize = 500L * 1024 * 1024;
    public const int ChunkSize = 64 * 1024;
    public const int MaxPreviewSize = 256 * 1024;
    private readonly ChatDbContext _db;
    private readonly string _root;
    private readonly Dictionary<string, Upload> _uploads = new();
    private sealed class Upload : IDisposable
    {
        public required string Path { get; init; }
        public required FileStream Stream { get; init; }
        public required string Name { get; init; }
        public required string ContentType { get; init; }
        public required int ConversationId { get; init; }
        public required long Size { get; init; }
        public required byte[] Preview { get; init; }
        public IncrementalHash Hash { get; } = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public void Dispose() { Stream.Dispose(); Hash.Dispose(); if (File.Exists(Path)) File.Delete(Path); }
    }
    public FileService(ChatDbContext db)
    {
        _db = db;
        _root = Environment.GetEnvironmentVariable("CHAT_STORAGE_PATH") ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_root);
    }
    public async Task<NetworkMessage> HandleAsync(int userId, string senderName, NetworkMessage request)
    {
        try
        {
            var p = request.Payload ?? throw new InvalidOperationException("Missing transfer payload.");
            object result;
            switch (request.Type)
            {
                case MessageType.UploadStart:
                {
                    int cid = request.ConversationId ?? 0;
                    await RequireMemberAsync(userId, cid);
                    long size = p.GetProperty("fileSize").GetInt64();
                    if (size < 0 || size > MaxFileSize) throw new InvalidOperationException("File limit: 500 MB.");
                    if (_uploads.Count >= 3) throw new InvalidOperationException("Maximum 3 concurrent uploads.");
                    string name = Path.GetFileName(p.GetProperty("fileName").GetString()!.Replace('\\', '/'));
                    if (string.IsNullOrWhiteSpace(name) || name.Length > 255) throw new InvalidOperationException("Invalid file name.");
                    byte[] preview = p.TryGetProperty("previewBase64", out var pv) && pv.ValueKind == JsonValueKind.String
                        ? Convert.FromBase64String(pv.GetString()!) : [];
                    if (preview.Length > MaxPreviewSize) throw new InvalidOperationException("Preview too large.");
                    string id = Guid.NewGuid().ToString("N");
                    string path = Path.Combine(_root, id + ".part");
                    _uploads.Add(id, new Upload
                    {
                        Path = path, Name = name, Size = size, ConversationId = cid, Preview = preview,
                        ContentType = p.GetProperty("contentType").GetString() ?? "application/octet-stream",
                        Stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, ChunkSize, true)
                    });
                    result = new { transferId = id };
                    break;
                }
                case MessageType.UploadChunk:
                {
                    var upload = GetUpload(p);
                    long offset = p.GetProperty("offset").GetInt64();
                    byte[] bytes = Convert.FromBase64String(p.GetProperty("dataBase64").GetString()!);
                    if (offset != upload.Stream.Position || bytes.Length == 0 || bytes.Length > ChunkSize || offset + bytes.Length > upload.Size)
                        throw new InvalidOperationException("Invalid chunk offset or size.");
                    await upload.Stream.WriteAsync(bytes);
                    upload.Hash.AppendData(bytes);
                    result = new { offset = upload.Stream.Position };
                    break;
                }
                case MessageType.UploadCancel:
                    if (_uploads.Remove(p.GetProperty("transferId").GetString()!, out var cancelled)) cancelled.Dispose();
                    result = new { cancelled = true };
                    break;
                case MessageType.UploadFinish:
                {
                    var upload = GetUpload(p);
                    if (upload.Stream.Position != upload.Size) throw new InvalidOperationException("Incomplete file.");
                    string hash = Convert.ToHexString(upload.Hash.GetHashAndReset());
                    if (!string.Equals(hash, p.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("File checksum mismatch.");
                    await upload.Stream.FlushAsync();
                    await upload.Stream.DisposeAsync();
                    await RequireMemberAsync(userId, upload.ConversationId);
                    var message = new Message
                    {
                        ConversationId = upload.ConversationId, SenderId = userId,
                        Content = upload.Name, MessageType = upload.Preview.Length > 0 ? "image" : "file", SentAt = DateTime.UtcNow
                    };
                    var attachment = new Attachment
                    {
                        Message = message, FileName = upload.Name, FileSize = upload.Size,
                        ContentType = upload.ContentType, FileData = []
                    };
                    await using var transaction = await _db.Database.BeginTransactionAsync();
                    _db.Attachments.Add(attachment);
                    string? finalPath = null;
                    try
                    {
                        await _db.SaveChangesAsync();
                        finalPath = AttachmentPath(attachment.AttachmentId);
                        File.Move(upload.Path, finalPath);
                        await File.WriteAllTextAsync(finalPath + ".sha256", hash);
                        if (upload.Preview.Length > 0) await File.WriteAllBytesAsync(finalPath + ".preview", upload.Preview);
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        if (finalPath != null)
                            foreach (var path in new[] { finalPath, finalPath + ".sha256", finalPath + ".preview" })
                                if (File.Exists(path)) File.Delete(path);
                        _db.ChangeTracker.Clear();
                        throw;
                    }
                    _uploads.Remove(p.GetProperty("transferId").GetString()!);
                    upload.Dispose();
                    return new NetworkMessage
                    {
                        Type = MessageType.BroadcastMessage, ConversationId = message.ConversationId,
                        SenderId = userId, SenderName = senderName, Content = message.Content, Timestamp = message.SentAt,
                        Payload = JsonSerializer.SerializeToElement(new
                        {
                            messageId = message.MessageId, messageType = message.MessageType,
                            attachmentId = attachment.AttachmentId, fileName = attachment.FileName,
                            fileSize = attachment.FileSize, contentType = attachment.ContentType
                        })
                    };
                }
                case MessageType.DownloadChunk:
                {
                    long id = p.GetProperty("attachmentId").GetInt64();
                    var info = await _db.Attachments.AsNoTracking().Where(a => a.AttachmentId == id)
                        .Select(a => new { a.FileSize, a.Message.ConversationId }).SingleOrDefaultAsync()
                        ?? throw new InvalidOperationException("Attachment not found.");
                    await RequireMemberAsync(userId, info.ConversationId);
                    bool preview = p.TryGetProperty("preview", out var pr) && pr.GetBoolean();
                    long offset = p.GetProperty("offset").GetInt64();
                    string path = AttachmentPath(id);
                    if (preview) path += ".preview";
                    byte[] bytes;
                    long total;
                    string? hash = null;
                    if (File.Exists(path))
                    {
                        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, true);
                        total = stream.Length;
                        if (offset < 0 || offset > total) throw new InvalidOperationException("Invalid download offset.");
                        stream.Position = offset;
                        bytes = new byte[(int)Math.Min(preview ? MaxPreviewSize : ChunkSize, total - offset)];
                        await stream.ReadExactlyAsync(bytes);
                        if (!preview && offset == 0) hash = await File.ReadAllTextAsync(path + ".sha256");
                    }
                    else
                    {
                        // Lab1 compatibility (old files were limited to 10 MB).
                        var legacy = await _db.Attachments.AsNoTracking().Where(a => a.AttachmentId == id)
                            .Select(a => a.FileData).SingleAsync();
                        if (legacy.Length == 0 && info.FileSize > 0) throw new InvalidOperationException("File missing on server.");
                        total = legacy.LongLength;
                        if (preview && total > MaxPreviewSize) throw new InvalidOperationException("Download this legacy image to view it.");
                        if (offset < 0 || offset > total) throw new InvalidOperationException("Invalid download offset.");
                        bytes = legacy.AsSpan((int)offset, (int)Math.Min(preview ? MaxPreviewSize : ChunkSize, total - offset)).ToArray();
                        if (offset == 0) hash = Convert.ToHexString(SHA256.HashData(legacy));
                    }
                    result = new { offset, total, sha256 = hash, dataBase64 = Convert.ToBase64String(bytes) };
                    break;
                }
                default: throw new InvalidOperationException("Please upgrade to the Lab2 client.");
            }
            return new NetworkMessage { Type = MessageType.TransferResponse, RequestId = request.RequestId, Payload = JsonSerializer.SerializeToElement(result) };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = NetworkMessage.CreateError(ex.Message);
            error.RequestId = request.RequestId;
            return error;
        }
    }
    private Upload GetUpload(JsonElement p) => _uploads.TryGetValue(p.GetProperty("transferId").GetString()!, out var upload)
        ? upload : throw new InvalidOperationException("Upload not found.");
    private string AttachmentPath(long id) => Path.Combine(_root, id + ".bin");
    private async Task RequireMemberAsync(int userId, int cid)
    {
        if (!await _db.ConversationMembers.AnyAsync(m => m.UserId == userId && m.ConversationId == cid))
            throw new InvalidOperationException("You are not a member of this conversation.");
    }
    public void Dispose() { foreach (var upload in _uploads.Values) upload.Dispose(); _uploads.Clear(); }
}
