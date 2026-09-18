using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ChatClient.Services;

public partial class ChatClientService
{
    public const long MaxFileSize = 500L * 1024 * 1024;
    private const int ChunkSize = 64 * 1024;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<NetworkMessage>> _pending = new();

    private async Task<JsonElement> RequestAsync(MessageType type, object payload, int? conversationId = null, CancellationToken ct = default)
    {
        string id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<NetworkMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await SendAsync(new NetworkMessage { Type = type, RequestId = id, ConversationId = conversationId,
                Payload = JsonSerializer.SerializeToElement(payload) }, ct).ConfigureAwait(false);
            var response = await completion.Task.WaitAsync(TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
            return response.Payload ?? throw new IOException("Missing server response.");
        }
        finally { _pending.TryRemove(id, out _); }
    }

    public async Task SendFileAsync(int conversationId, string filePath, string messageType = "file",
        byte[]? preview = null, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, true);
        if (file.Length > MaxFileSize) throw new InvalidOperationException("File limit: 500 MB.");
        var start = await RequestAsync(MessageType.UploadStart, new
        {
            fileName = Path.GetFileName(filePath), fileSize = file.Length,
            contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
                ".bmp" => "image/bmp", _ => "application/octet-stream"
            },
            previewBase64 = preview == null ? null : Convert.ToBase64String(preview)
        }, conversationId, ct).ConfigureAwait(false);
        string transferId = start.GetProperty("transferId").GetString()!;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[ChunkSize];
            long offset = 0;
            int count;
            while ((count = await file.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, count);
                var ack = await RequestAsync(MessageType.UploadChunk, new
                {
                    transferId, offset, dataBase64 = Convert.ToBase64String(buffer, 0, count)
                }, ct: ct).ConfigureAwait(false);
                offset += count;
                if (ack.GetProperty("offset").GetInt64() != offset) throw new IOException("Upload offset mismatch.");
                progress?.Report(file.Length == 0 ? 0 : offset * 99.0 / file.Length);
            }
            await RequestAsync(MessageType.UploadFinish, new { transferId, sha256 = Convert.ToHexString(hash.GetHashAndReset()) }, ct: ct).ConfigureAwait(false);
            progress?.Report(100);
        }
        catch
        {
            try { await RequestAsync(MessageType.UploadCancel, new { transferId }).ConfigureAwait(false); }
            catch { /* Disconnect cleanup also removes partial uploads. */ }
            throw;
        }
    }

    public async Task<byte[]> GetPreviewAsync(long attachmentId, CancellationToken ct = default)
    {
        var response = await RequestAsync(MessageType.DownloadChunk, new { attachmentId, offset = 0, preview = true }, ct: ct).ConfigureAwait(false);
        return Convert.FromBase64String(response.GetProperty("dataBase64").GetString()!);
    }

    public async Task DownloadFileAsync(long attachmentId, string destination, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        // Keep an existing destination intact until the entire download has been verified.
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, ChunkSize, true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long offset = 0, total = -1;
                string? expectedHash = null;
                do
                {
                    var response = await RequestAsync(MessageType.DownloadChunk, new { attachmentId, offset }, ct: ct).ConfigureAwait(false);
                    long declared = response.GetProperty("total").GetInt64();
                    if (declared < 0 || declared > MaxFileSize || (total >= 0 && total != declared) || response.GetProperty("offset").GetInt64() != offset)
                        throw new IOException("Invalid download size or offset.");
                    total = declared;
                    if (offset == 0) expectedHash = response.GetProperty("sha256").GetString();
                    byte[] bytes = Convert.FromBase64String(response.GetProperty("dataBase64").GetString()!);
                    if (bytes.Length > ChunkSize || offset + bytes.Length > total || (bytes.Length == 0 && offset < total))
                        throw new IOException("Invalid download chunk.");
                    await file.WriteAsync(bytes, ct).ConfigureAwait(false);
                    hash.AppendData(bytes);
                    offset += bytes.Length;
                    progress?.Report(total == 0 ? 0 : offset * 99.0 / total);
                } while (offset < total);
                if (!string.Equals(expectedHash, Convert.ToHexString(hash.GetHashAndReset()), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("File checksum mismatch.");
                await file.FlushAsync(ct).ConfigureAwait(false);
            }
            File.Move(temporary, destination, true);
            progress?.Report(100);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
