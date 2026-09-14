using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services
{
    // ── Định nghĩa Protocol (phải khớp với ChatServer) ──────────────────────

    /// <summary>Loại gói tin - phải khớp với enum MessageType trên Server.</summary>
    public enum MessageType
    {
        Login = 1, Register = 2, AuthResponse = 3,
        SendMessage = 10, BroadcastMessage = 11,
        GetHistory = 12, HistoryResponse = 13,
        SendFile = 14,
        GetConversations = 20, ConversationsResponse = 21,
        CreateConversation = 22,
        UserOnline = 30, UserOffline = 31,
        GetOnlineUsers = 32, OnlineUsersResponse = 33,
        Ping = 90, Pong = 91, Error = 99,
    }

    /// <summary>Cấu trúc gói tin JSON - phải khớp với NetworkMessage trên Server.</summary>
    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public int? SenderId { get; set; }
        public string? SenderName { get; set; }
        public int? ConversationId { get; set; }
        public string? Content { get; set; }
        public JsonElement? Payload { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public string Serialize() =>
            JsonSerializer.Serialize(this, Options) + "\n";

        public static NetworkMessage? Deserialize(string json)
        {
            try { return JsonSerializer.Deserialize<NetworkMessage>(json, Options); }
            catch { return null; }
        }
    }

    // ── ChatClientService ────────────────────────────────────────────────────

    /// <summary>
    /// Dịch vụ quản lý kết nối TCP của ChatClient đến ChatServer.
    /// Chịu trách nhiệm:
    ///   - Kết nối / Ngắt kết nối đến Server.
    ///   - Gửi các gói NetworkMessage (Login, Register, SendMessage, GetHistory...).
    ///   - Vòng lặp đọc phản hồi ngầm từ Server (background receive loop).
    ///   - Phát ra sự kiện cho MainWindow để cập nhật giao diện.
    /// Lưu ý: Không lưu dữ liệu vào DB - Client chỉ hiển thị, DB do Server quản lý.
    /// </summary>
    public class ChatClientService : IDisposable
    {
        // ── Sự kiện thông báo lên giao diện ──────────────────────────────────

        /// <summary>Kết nối đến Server thành công.</summary>
        public event Action? OnConnected;

        /// <summary>Ngắt kết nối khỏi Server (do người dùng chủ động hoặc lỗi mạng).</summary>
        public event Action<string>? OnDisconnected;

        /// <summary>Xác thực (Login/Register) thành công, kèm thông tin người dùng.</summary>
        public event Action<int, string>? OnAuthSuccess;

        /// <summary>Xác thực thất bại, kèm thông báo lỗi.</summary>
        public event Action<string>? OnAuthFailed;

        /// <summary>Nhận được tin nhắn mới (BroadcastMessage) từ Server.</summary>
        public event Action<NetworkMessage>? OnMessageReceived;

        /// <summary>Nhận được lịch sử tin nhắn (HistoryResponse) từ Server.</summary>
        public event Action<NetworkMessage>? OnHistoryReceived;

        /// <summary>Nhận được danh sách cuộc hội thoại (ConversationsResponse) từ Server.</summary>
        public event Action<NetworkMessage>? OnConversationsReceived;

        /// <summary>Nhận được danh sách người dùng online (OnlineUsersResponse) từ Server.</summary>
        public event Action<NetworkMessage>? OnOnlineUsersReceived;

        /// <summary>Một người dùng khác vừa online.</summary>
        public event Action<NetworkMessage>? OnUserOnline;

        /// <summary>Một người dùng khác vừa offline.</summary>
        public event Action<NetworkMessage>? OnUserOffline;

        /// <summary>Nhận được gói Error từ Server.</summary>
        public event Action<string>? OnError;

        // ── Trạng thái kết nối ───────────────────────────────────────────────

        /// <summary>UserId sau khi xác thực thành công.</summary>
        public int? CurrentUserId { get; private set; }

        /// <summary>DisplayName sau khi xác thực thành công.</summary>
        public string? CurrentDisplayName { get; private set; }

        /// <summary>Cho biết đang kết nối tới Server hay chưa.</summary>
        public bool IsConnected => _tcpClient?.Connected == true;

        /// <summary>Cho biết đã xác thực thành công hay chưa.</summary>
        public bool IsAuthenticated => CurrentUserId.HasValue;

        // ── Tài nguyên mạng ──────────────────────────────────────────────────
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        // ── Kết nối / Ngắt kết nối ───────────────────────────────────────────

        /// <summary>
        /// Kết nối bất đồng bộ đến ChatServer tại host:port được chỉ định.
        /// Sau khi kết nối thành công, bắt đầu vòng lặp đọc phản hồi ngầm.
        /// </summary>
        /// <param name="host">Địa chỉ IP hoặc hostname của Server.</param>
        /// <param name="port">Cổng TCP Server đang lắng nghe.</param>
        public async Task ConnectAsync(string host, int port)
        {
            // Giải phóng kết nối cũ nếu còn tồn tại
            CleanupResources();

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);

            _stream = _tcpClient.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);

            _cts = new CancellationTokenSource();

            // Thông báo kết nối thành công
            OnConnected?.Invoke();

            // Bắt đầu vòng lặp đọc phản hồi ngầm
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Ngắt kết nối chủ động khỏi Server và giải phóng tài nguyên.
        /// </summary>
        public void Disconnect()
        {
            CleanupResources();
            OnDisconnected?.Invoke("Disconnected by user.");
        }

        // ── Xác thực ─────────────────────────────────────────────────────────

        /// <summary>Gửi yêu cầu đăng nhập đến Server với username và password.</summary>
        public async Task LoginAsync(string username, string password)
        {
            var payload = new { username, password };

            await SendAsync(new NetworkMessage
            {
                Type = MessageType.Login,
                Payload = JsonSerializer.SerializeToElement(payload),
            });
        }

        /// <summary>Gửi yêu cầu đăng ký tài khoản mới đến Server.</summary>
        public async Task RegisterAsync(
            string username,
            string password,
            string displayName)
        {
            var payload = new { username, password, displayName };

            await SendAsync(new NetworkMessage
            {
                Type = MessageType.Register,
                Payload = JsonSerializer.SerializeToElement(payload),
            });
        }

        // ── Tin nhắn ─────────────────────────────────────────────────────────

        /// <summary>Gửi tin nhắn text đến một cuộc hội thoại cụ thể.</summary>
        public async Task SendMessageAsync(int conversationId, string content)
        {
            await SendAsync(new NetworkMessage
            {
                Type = MessageType.SendMessage,
                ConversationId = conversationId,
                Content = content,
            });
        }

        /// <summary>
        /// Yêu cầu Server gửi lịch sử tin nhắn của một cuộc hội thoại.
        /// </summary>
        /// <param name="conversationId">ID cuộc hội thoại cần tải lịch sử.</param>
        /// <param name="offset">Vị trí bắt đầu (dùng cho phân trang, mặc định 0).</param>
        public async Task GetHistoryAsync(int conversationId, int offset = 0)
        {
            var payload = new { conversationId, offset };

            await SendAsync(new NetworkMessage
            {
                Type = MessageType.GetHistory,
                Payload = JsonSerializer.SerializeToElement(payload),
            });
        }

        // ── Cuộc hội thoại ───────────────────────────────────────────────────

        /// <summary>Yêu cầu Server gửi danh sách cuộc hội thoại của người dùng hiện tại.</summary>
        public async Task GetConversationsAsync()
        {
            await SendAsync(new NetworkMessage
            {
                Type = MessageType.GetConversations,
            });
        }

        /// <summary>
        /// Yêu cầu Server tạo cuộc hội thoại mới.
        /// </summary>
        /// <param name="type">"direct" (1-1) hoặc "group" (nhóm).</param>
        /// <param name="name">Tên nhóm (dùng cho type="group").</param>
        /// <param name="memberIds">Danh sách UserId thành viên tham gia.</param>
        public async Task CreateConversationAsync(
            string type,
            string? name,
            int[] memberIds)
        {
            var payload = new { type, name, memberIds };

            await SendAsync(new NetworkMessage
            {
                Type = MessageType.CreateConversation,
                Payload = JsonSerializer.SerializeToElement(payload),
            });
        }

        // ── Người dùng online ─────────────────────────────────────────────────

        /// <summary>Yêu cầu Server gửi danh sách người dùng đang online.</summary>
        public async Task GetOnlineUsersAsync()
        {
            await SendAsync(new NetworkMessage
            {
                Type = MessageType.GetOnlineUsers,
            });
        }

        /// <summary>
        /// Gửi tập tin hoặc hình ảnh đến một cuộc hội thoại.
        /// Đọc file và mã hoá Base64 để gửi qua TCP.
        /// </summary>
        public async Task SendFileAsync(int conversationId, string filePath, string messageType = "file")
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File does not exist.", filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("File size exceeds 10 MB limit.");

            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            string base64 = Convert.ToBase64String(bytes);
            string fileName = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            string contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };

            var payload = new
            {
                fileName,
                fileSize = fileInfo.Length,
                contentType,
                messageType,
                dataBase64 = base64,
            };

            await SendAsync(new NetworkMessage
            {
                Type = MessageType.SendFile,
                ConversationId = conversationId,
                Content = fileName,
                Payload = JsonSerializer.SerializeToElement(payload),
            });
        }

        // ── Vòng lặp đọc phản hồi ngầm ───────────────────────────────────────

        /// <summary>
        /// Vòng lặp chạy ngầm (background Task), liên tục đọc gói tin từ Server
        /// và phát ra sự kiện tương ứng để giao diện WPF cập nhật.
        /// Kết thúc khi Server đóng kết nối hoặc CancellationToken bị huỷ.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _reader is not null)
                {
                    string? line = await _reader.ReadLineAsync(ct);
                    if (line is null) break; // Server đóng kết nối

                    var message = NetworkMessage.Deserialize(line);
                    if (message is null) continue; // Gói tin không hợp lệ, bỏ qua

                    ProcessReceivedMessage(message);
                }
            }
            catch (OperationCanceledException) { /* Bị cancel chủ động */ }
            catch (Exception ex)
            {
                OnDisconnected?.Invoke($"Connection lost: {ex.Message}");
            }
            finally
            {
                CleanupResources();
            }
        }

        /// <summary>
        /// Xử lý gói tin nhận được từ Server và phát sự kiện tương ứng.
        /// </summary>
        private void ProcessReceivedMessage(NetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.AuthResponse:
                    HandleAuthResponse(message);
                    break;

                case MessageType.BroadcastMessage:
                    OnMessageReceived?.Invoke(message);
                    break;

                case MessageType.HistoryResponse:
                    OnHistoryReceived?.Invoke(message);
                    break;

                case MessageType.ConversationsResponse:
                    OnConversationsReceived?.Invoke(message);
                    break;

                case MessageType.OnlineUsersResponse:
                    OnOnlineUsersReceived?.Invoke(message);
                    break;

                case MessageType.UserOnline:
                    OnUserOnline?.Invoke(message);
                    break;

                case MessageType.UserOffline:
                    OnUserOffline?.Invoke(message);
                    break;

                case MessageType.Pong:
                    // Heartbeat phản hồi - không cần xử lý UI
                    break;

                case MessageType.Error:
                    OnError?.Invoke(message.Content ?? "Unknown error from server.");
                    break;
            }
        }

        /// <summary>
        /// Xử lý phản hồi xác thực (AuthResponse) từ Server.
        /// Cập nhật CurrentUserId/DisplayName nếu thành công.
        /// </summary>
        private void HandleAuthResponse(NetworkMessage message)
        {
            bool success = false;
            string? errorMessage = null;

            if (message.Payload.HasValue)
            {
                var payload = message.Payload.Value;

                if (payload.TryGetProperty("success", out var successEl))
                    success = successEl.GetBoolean();

                if (!success &&
                    payload.TryGetProperty("errorMessage", out var errEl))
                    errorMessage = errEl.GetString();

                if (success)
                {
                    int userId = 0;
                    string displayName = "Unknown";

                    if (payload.TryGetProperty("userId", out var uidEl))
                        uidEl.TryGetInt32(out userId);

                    if (payload.TryGetProperty("displayName", out var dnEl))
                        displayName = dnEl.GetString() ?? "Unknown";

                    CurrentUserId = userId;
                    CurrentDisplayName = displayName;
                    OnAuthSuccess?.Invoke(userId, displayName);
                    return;
                }
            }

            // Xác thực thất bại
            OnAuthFailed?.Invoke(
                errorMessage ?? message.Content ?? "Authentication failed.");
        }

        // ── Gửi gói tin ──────────────────────────────────────────────────────

        /// <summary>Gửi một NetworkMessage đến Server qua TCP stream.</summary>
        private async Task SendAsync(NetworkMessage message)
        {
            if (_stream is null || !IsConnected)
                throw new InvalidOperationException(
                    "Not connected to server.");

            string json = message.Serialize();
            byte[] data = Encoding.UTF8.GetBytes(json);
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();
        }

        // ── Giải phóng tài nguyên ─────────────────────────────────────────────

        private void CleanupResources()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _reader?.Dispose();
            _reader = null;

            _stream?.Dispose();
            _stream = null;

            _tcpClient?.Dispose();
            _tcpClient = null;

            CurrentUserId = null;
            CurrentDisplayName = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CleanupResources();
        }
    }
}
