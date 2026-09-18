using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ChatClient.Services;
using ChatServer.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServerHost = ChatServer.Networking.ChatServer;

if (args.Contains("--ui")) { UiSmoke.Run(); return; }

// Real TCP + SQL integration checks. Uses an isolated database, never the application's database.
string run = Guid.NewGuid().ToString("N");
string work = Path.GetFullPath(Path.Combine(".lab2-tests", run));
Directory.CreateDirectory(work);
Environment.SetEnvironmentVariable("CHAT_STORAGE_PATH", Path.Combine(work, "uploads"));
var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("LAB2_TEST_SQL")
    ?? "Server=localhost;Integrated Security=true;Encrypt=false;TrustServerCertificate=true")
{ InitialCatalog = "ChatLab2Test_" + run, ConnectTimeout = 5 };
var options = new DbContextOptionsBuilder<ChatDbContext>().UseSqlServer(connection.ConnectionString).Options;
await using var db = new ChatDbContext(options);
using var stopping = new CancellationTokenSource();
Task? serverTask = null;
var peers = new List<Peer>();
bool databaseCreated = false;
try
{
    await db.Database.EnsureCreatedAsync();
    databaseCreated = true;
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    var server = new ServerHost("127.0.0.1", port, options);
    serverTask = server.StartAsync(stopping.Token);
    for (int i = 0; i < 4; i++)
    {
        var peer = new Peer(); peers.Add(peer);
        await peer.Client.ConnectAsync("127.0.0.1", port);
        await peer.Client.RegisterAsync("user" + i, "lab2-test-password", "Tester " + i);
        await peer.Auth.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    var a = peers[0]; var b = peers[1]; var c = peers[2]; var outsider = peers[3];
    await a.Client.CreateConversationAsync("group", "Lab2 integration", []);
    int group = (await a.Conversations.Next(m => m.ConversationId.HasValue)).ConversationId!.Value;
    await Task.WhenAll(b.Client.JoinConversationAsync(group), c.Client.JoinConversationAsync(group));
    await b.Conversations.Next(m => m.ConversationId == group);
    await c.Conversations.Next(m => m.ConversationId == group);
    await b.Client.JoinConversationAsync(group);
    await b.Conversations.Next(m => m.ConversationId == group);
    Check(await db.ConversationMembers.CountAsync(m => m.ConversationId == group) == 3, "Concurrent joins and repeated join: exactly 3 members");

    string text = "Xin chào 😀 ❤️ 🎉";
    await a.Client.SendMessageAsync(group, text);
    await Task.WhenAll(a.Messages.Next(m => m.Content == text), b.Messages.Next(m => m.Content == text), c.Messages.Next(m => m.Content == text));
    Check(true, "Unicode/color-emoji text reaches all three members");
    await outsider.Client.SendMessageAsync(group, "unauthorized");
    await outsider.Errors.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    Check(!await db.Messages.AnyAsync(m => m.Content == "unauthorized"), "Non-member cannot send to group");

    string source = Path.Combine(work, "500mb.bin");
    long size = args.Contains("--quick") ? 2L * 1024 * 1024 : 500L * 1024 * 1024;
    await using (var output = new FileStream(source, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
    {
        byte[] block = new byte[1024 * 1024]; RandomNumberGenerator.Fill(block);
        for (long written = 0; written < size; written += block.Length) await output.WriteAsync(block);
    }
    var timer = Stopwatch.StartNew();
    var upload = a.Client.SendFileAsync(group, source);
    await a.Client.SendMessageAsync(group, "chat during upload");
    await c.Messages.Next(m => m.Content == "chat during upload");
    Check(!upload.IsCompleted, "Chat continues while upload is active");
    await upload;
    var attachment = await b.Messages.Next(m => m.Content == "500mb.bin");
    long attachmentId = attachment.Payload!.Value.GetProperty("attachmentId").GetInt64();
    Check(await db.Attachments.Where(x => x.AttachmentId == attachmentId).Select(x => x.FileData.Length).SingleAsync() == 0, "Large attachment body is on disk, not in SQL");
    string downloaded = Path.Combine(work, "download.bin");
    string secondCopy = Path.Combine(work, "download2.bin");
    await Task.WhenAll(b.Client.DownloadFileAsync(attachmentId, downloaded), c.Client.DownloadFileAsync(attachmentId, secondCopy));
    Check(await Digest(source) == await Digest(downloaded) && await Digest(source) == await Digest(secondCopy), "Two simultaneous downloads match source SHA-256");
    Console.WriteLine($"Transfer size: {size / (1024 * 1024)} MiB; upload + two downloads: {timer.Elapsed.TotalSeconds:F1}s; peak working set: {Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024)} MiB");
    await Throws(() => outsider.Client.DownloadFileAsync(attachmentId, Path.Combine(work, "denied.bin")), "Non-member cannot download attachment");
    await b.Client.GetHistoryAsync(group);
    var history = await b.History.Next(m => m.ConversationId == group);
    Check(!history.Serialize().Contains("dataBase64") && history.Serialize().Length < 20000, "History contains metadata only");

    string empty = Path.Combine(work, "empty.bin"); await File.WriteAllBytesAsync(empty, []);
    await a.Client.SendFileAsync(group, empty);
    var emptyMessage = await b.Messages.Next(m => m.Content == "empty.bin");
    await b.Client.DownloadFileAsync(emptyMessage.Payload!.Value.GetProperty("attachmentId").GetInt64(), Path.Combine(work, "empty-copy.bin"));
    Check(new FileInfo(Path.Combine(work, "empty-copy.bin")).Length == 0, "Empty file round-trip");

    string image = Path.Combine(work, "preview.png");
    byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    await File.WriteAllBytesAsync(image, png);
    await a.Client.SendFileAsync(group, image, "image", png);
    var imageMessage = await c.Messages.Next(m => m.Content == "preview.png");
    var previewBytes = await c.Client.GetPreviewAsync(imageMessage.Payload!.Value.GetProperty("attachmentId").GetInt64());
    Check(png.SequenceEqual(previewBytes), "Image thumbnail round-trip");

    using (var cancel = new CancellationTokenSource())
    {
        var progress = new InlineProgress(_ => cancel.Cancel());
        await Throws(() => a.Client.SendFileAsync(group, source, progress: progress, ct: cancel.Token), "Upload cancellation");
    }
    Check(!Directory.EnumerateFiles(Path.Combine(work, "uploads"), "*.part").Any(), "Cancelled upload removes partial server file");
    string kept = Path.Combine(work, "keep.txt"); await File.WriteAllTextAsync(kept, "keep original");
    using (var cancel = new CancellationTokenSource())
        await Throws(() => b.Client.DownloadFileAsync(attachmentId, kept, new InlineProgress(_ => cancel.Cancel()), cancel.Token), "Download cancellation");
    Check(await File.ReadAllTextAsync(kept) == "keep original", "Cancelled download preserves existing destination");
    Check(!Directory.EnumerateFiles(work, "*.part").Any(), "Cancelled download removes partial client file");

    string oversized = Path.Combine(work, "oversized.bin");
    using (var file = File.Create(oversized)) file.SetLength(ChatClientService.MaxFileSize + 1);
    await Throws(() => a.Client.SendFileAsync(group, oversized), "Client rejects more than 500 MiB");
    // Exercise invalid/hostile requests against the server, independently of client validation.
    using (var raw = new TcpClient())
    {
        await raw.ConnectAsync("127.0.0.1", port);
        using var reader = new StreamReader(raw.GetStream());
        async Task<NetworkMessage> Request(MessageType type, object payload, int? cid = null)
        {
            string id = Guid.NewGuid().ToString("N");
            await raw.GetStream().WriteAsync(Encoding.UTF8.GetBytes(new NetworkMessage { Type = type, RequestId = id, ConversationId = cid, Payload = JsonSerializer.SerializeToElement(payload) }.Serialize()));
            while (true)
            {
                var message = NetworkMessage.Deserialize((await reader.ReadLineAsync())!)!;
                if (message.RequestId == id || message.Type == MessageType.AuthResponse) return message;
            }
        }
        await Request(MessageType.Login, new { username = "user0", password = "lab2-test-password" });
        var denied = await Request(MessageType.UploadStart, new { fileName = "too-big", fileSize = ChatClientService.MaxFileSize + 1, contentType = "application/octet-stream" }, group);
        Check(denied.Type == MessageType.Error, "Server independently rejects more than 500 MiB");
        var started = await Request(MessageType.UploadStart, new { fileName = "bad", fileSize = 3, contentType = "application/octet-stream" }, group);
        string transferId = started.Payload!.Value.GetProperty("transferId").GetString()!;
        Check((await Request(MessageType.UploadChunk, new { transferId, offset = 1, dataBase64 = "AQID" })).Type == MessageType.Error, "Server rejects out-of-order chunks");
        await Request(MessageType.UploadChunk, new { transferId, offset = 0, dataBase64 = "AQID" });
        Check((await Request(MessageType.UploadFinish, new { transferId, sha256 = "wrong" })).Type == MessageType.Error, "Server rejects corrupt checksum");
        await Request(MessageType.UploadCancel, new { transferId });
    }
    await a.Client.CreateConversationAsync("direct", null, [b.Client.CurrentUserId!.Value]);
    int direct = (await a.Conversations.Next(m => m.ConversationId.HasValue && m.ConversationId != group)).ConversationId!.Value;
    await outsider.Client.JoinConversationAsync(direct);
    await outsider.Errors.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    Check(!await db.ConversationMembers.AnyAsync(m => m.ConversationId == direct && m.UserId == outsider.Client.CurrentUserId), "Direct chat cannot be joined by code");
    Console.WriteLine("ALL LAB2 INTEGRATION CHECKS PASSED");
}
finally
{
    foreach (var peer in peers) peer.Client.Dispose();
    stopping.Cancel();
    if (serverTask != null) await serverTask;
    if (databaseCreated) await db.Database.EnsureDeletedAsync();
    // Only the generated GUID test directory is eligible for deletion.
    if (Path.GetFileName(work) == run && Path.GetFileName(Path.GetDirectoryName(work)) == ".lab2-tests") Directory.Delete(work, true);
}
static void Check(bool success, string name) { if (!success) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
static async Task Throws(Func<Task> action, string name)
{
    try { await action(); } catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException) { Check(true, name); return; }
    throw new Exception("Expected failure: " + name);
}
static async Task<string> Digest(string path) { await using var file = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(file)); }
sealed class InlineProgress(Action<double> action) : IProgress<double> { public void Report(double value) => action(value); }
sealed class Inbox
{
    private readonly Channel<NetworkMessage> _channel = Channel.CreateUnbounded<NetworkMessage>();
    public void Add(NetworkMessage message) => _channel.Writer.TryWrite(message);
    public async Task<NetworkMessage> Next(Func<NetworkMessage, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true) { var message = await _channel.Reader.ReadAsync(timeout.Token); if (predicate(message)) return message; }
    }
}
sealed class Peer
{
    public ChatClientService Client { get; } = new();
    public TaskCompletionSource<int> Auth { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Inbox Messages { get; } = new();
    public Inbox Conversations { get; } = new();
    public Inbox History { get; } = new();
    public Channel<string> Errors { get; } = Channel.CreateUnbounded<string>();
    public Peer()
    {
        Client.OnAuthSuccess += (id, _) => Auth.TrySetResult(id);
        Client.OnAuthFailed += error => Auth.TrySetException(new Exception(error));
        Client.OnMessageReceived += Messages.Add;
        Client.OnConversationsReceived += Conversations.Add;
        Client.OnHistoryReceived += History.Add;
        Client.OnError += error => Errors.Writer.TryWrite(error);
    }
}
