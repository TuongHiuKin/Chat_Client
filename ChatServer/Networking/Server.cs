using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Protocol;
using ChatServer.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Networking
{
    /// <summary>
    /// TCP Chat Server - Lõi trung tâm quản lý tất cả kết nối Client.
    /// Chịu trách nhiệm:
    ///   - Lắng nghe kết nối mới (TcpListener).
    ///   - Quản lý danh sách tất cả ClientConnection đang hoạt động.
    ///   - Cung cấp phương thức Broadcast đến tất cả Client hoặc theo cuộc hội thoại.
    ///   - Cung cấp danh sách người dùng đang online.
    ///   - Tạo ChatDbContext riêng cho mỗi ClientHandler (DbContext không thread-safe).
    /// </summary>
    public class ChatServer
    {
        // ── Cấu hình ──────────────────────────────────────────────
        private readonly string _host;
        private readonly int _port;
        private readonly DbContextOptions<ChatDbContext> _dbOptions;

        // ── Trạng thái ────────────────────────────────────────────
        private TcpListener? _listener;
        private readonly ConcurrentDictionary<string, Task> _handlers = new();

        /// <summary>
        /// Danh sách các kết nối Client đang hoạt động.
        /// ConcurrentDictionary để an toàn khi nhiều task đọc/ghi đồng thời.
        /// Key = ConnectionId, Value = ClientConnection.
        /// </summary>
        private readonly ConcurrentDictionary<string, ClientConnection> _connections = new();

        public ChatServer(
            string host,
            int port,
            DbContextOptions<ChatDbContext> dbOptions)
        {
            _host = host;
            _port = port;
            _dbOptions = dbOptions;
        }

        // ── Khởi động / Dừng Server ───────────────────────────────

        /// <summary>
        /// Khởi động Server: Bind TcpListener và bắt đầu vòng lặp chấp nhận Client.
        /// Chạy cho đến khi CancellationToken bị huỷ.
        /// </summary>
        public async Task StartAsync(CancellationToken ct = default)
        {
            if (!IPAddress.TryParse(_host, out var ip))
                ip = IPAddress.Any;

            _listener = new TcpListener(ip, _port);
            _listener.Start();

            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║         CHAT SERVER STARTED          ║");
            Console.WriteLine("╠══════════════════════════════════════╣");
            Console.WriteLine($"║  Host : {_host,-28} ║");
            Console.WriteLine($"║  Port : {_port,-28} ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("Waiting for clients... (Ctrl+C to stop)");
            Console.WriteLine();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Chờ Client mới kết nối vào
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(ct);

                    // Tạo ClientConnection và đăng ký vào danh sách
                    var connection = new ClientConnection(tcpClient);
                    _connections[connection.ConnectionId] = connection;

                    // Tạo DbContext mới cho mỗi Client (mỗi handler có scope riêng)
                    var db = new ChatDbContext(_dbOptions);
                    var authService = new AuthenticationService(db);
                    var messageService = new MessageService(db);
                    var fileService = new FileService(db);

                    var handler = new ClientHandler(
                        connection,
                        this,
                        authService,
                        messageService,
                        fileService,
                        ct);

                    // Chạy handler độc lập trong Task riêng, không await
                    var handlerTask = Task.Run(async () =>
                    {
                        try { await handler.HandleAsync(); }
                        finally { await db.DisposeAsync(); }
                    });
                    _handlers[connection.ConnectionId] = handlerTask;
                    _ = handlerTask.ContinueWith(completed => { _handlers.TryRemove(connection.ConnectionId, out _); }, TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException)
            {
                // Server dừng bình thường
            }
            finally
            {
                _listener.Stop();
                foreach (var connection in _connections.Values) connection.Dispose();
                await Task.WhenAll(_handlers.Values);
                Console.WriteLine("Server stopped.");
            }
        }

        // ── Quản lý kết nối ───────────────────────────────────────

        /// <summary>Xoá một kết nối khỏi danh sách khi Client ngắt kết nối.</summary>
        public void RemoveConnection(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
        }

        // ── Broadcast ─────────────────────────────────────────────

        /// <summary>
        /// Gửi một gói tin đến tất cả Client đang kết nối, ngoại trừ một connectionId.
        /// Dùng cho các sự kiện UserOnline/UserOffline.
        /// </summary>
        public async Task BroadcastToAllExceptAsync(
            NetworkMessage message,
            string? excludeConnectionId = null)
        {
            var tasks = _connections.Values
                .Where(c => c.ConnectionId != excludeConnectionId
                         && c.IsAuthenticated)
                .Select(c => TrySendAsync(c, message));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gửi một gói tin đến tất cả thành viên đang online trong một cuộc hội thoại.
        /// Lấy danh sách UserId thành viên từ danh sách kết nối hiện tại (không query DB lại).
        /// </summary>
        public async Task BroadcastToConversationAsync(
            NetworkMessage message,
            int conversationId)
        {
            // Truy vấn thành viên cuộc hội thoại từ DB bằng một DbContext tạm thời
            using var db = new ChatDbContext(_dbOptions);
            var memberIds = await db.ConversationMembers
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .Select(m => m.UserId)
                .ToListAsync();

            // Gửi đến các kết nối thuộc thành viên đang online
            var tasks = _connections.Values
                .Where(c => c.IsAuthenticated &&
                            memberIds.Contains(c.AuthenticatedUserId!.Value))
                .Select(c => TrySendAsync(c, message));

            await Task.WhenAll(tasks);
        }

        // ── Người dùng online ─────────────────────────────────────

        /// <summary>
        /// Trả về danh sách người dùng đang kết nối và đã xác thực.
        /// Dùng cho gói GetOnlineUsers / OnlineUsersResponse.
        /// </summary>
        public IEnumerable<object> GetOnlineUsers()
        {
            return _connections.Values
                .Where(c => c.IsAuthenticated)
                .Select(c => new
                {
                    userId = c.AuthenticatedUserId!.Value,
                    displayName = c.DisplayName,
                })
                .ToList();
        }

        // ── Gửi an toàn (bắt exception) ───────────────────────────

        /// <summary>
        /// Gửi gói tin đến một Client, bắt ngoại lệ nếu kết nối đã đóng.
        /// Tránh crash toàn bộ broadcast khi một Client bị lỗi.
        /// </summary>
        private static async Task TrySendAsync(
            ClientConnection connection,
            NetworkMessage message)
        {
            try
            {
                await connection.SendAsync(message);
            }
            catch
            {
                // Bỏ qua lỗi gửi tới Client cụ thể (có thể đã ngắt kết nối)
            }
        }
    }
}
