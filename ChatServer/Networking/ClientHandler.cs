using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Protocol;
using ChatServer.Services;
using ChatServer.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Networking
{
    /// <summary>
    /// Xử lý toàn bộ luồng giao tiếp của một Client sau khi đã kết nối.
    /// Chịu trách nhiệm:
    ///   - Vòng lặp đọc gói tin (ReadLoop) cho đến khi Client ngắt kết nối.
    ///   - Điều phối (Route) từng gói tin đến đúng Service xử lý.
    ///   - Broadcast tin nhắn đến các Client khác trong cùng cuộc hội thoại.
    ///   - Dọn dẹp tài nguyên và cập nhật trạng thái Offline khi Client rời đi.
    /// </summary>
    public class ClientHandler
    {
        private readonly ClientConnection _connection;
        private readonly ChatServer _server;
        private readonly AuthenticationService _authService;
        private readonly MessageService _messageService;
        private readonly FileService _fileService;
        private readonly CancellationToken _serverCt;

        public ClientHandler(
            ClientConnection connection,
            ChatServer server,
            AuthenticationService authService,
            MessageService messageService,
            FileService fileService,
            CancellationToken serverCt)
        {
            _connection = connection;
            _server = server;
            _authService = authService;
            _messageService = messageService;
            _fileService = fileService;
            _serverCt = serverCt;
        }

        // ── Vòng lặp chính ────────────────────────────────────────

        /// <summary>
        /// Vòng lặp đọc gói tin từ Client cho đến khi Client ngắt kết nối
        /// hoặc Server bị tắt. Mỗi gói tin được xử lý bất đồng bộ.
        /// </summary>
        public async Task HandleAsync()
        {
            Console.WriteLine(
                $"[+] Client connected: {_connection.RemoteEndpoint} " +
                $"[{_connection.ConnectionId[..8]}]");

            try
            {
                while (!_serverCt.IsCancellationRequested)
                {
                    // Đọc gói tin tiếp theo từ stream
                    NetworkMessage? message = await _connection.ReadMessageAsync(_serverCt);

                    if (message is null)
                    {
                        // Client đóng kết nối bình thường
                        break;
                    }

                    // Điều phối gói tin đến đúng handler
                    await RouteMessageAsync(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Server đang tắt - không cần log
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[!] Client error [{_connection.ConnectionId[..8]}]: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        // ── Điều phối gói tin ─────────────────────────────────────

        /// <summary>
        /// Phân loại và điều phối gói tin đến đúng phương thức xử lý.
        /// Các gói tin yêu cầu xác thực sẽ bị từ chối nếu Client chưa Login.
        /// </summary>
        private async Task RouteMessageAsync(NetworkMessage message)
        {
            switch (message.Type)
            {
                // Xác thực - không cần đăng nhập trước
                case MessageType.Login:
                    await HandleLoginAsync(message);
                    break;

                case MessageType.Register:
                    await HandleRegisterAsync(message);
                    break;

                // Các tính năng yêu cầu xác thực
                case MessageType.SendMessage:
                    await RequireAuthAsync(() => HandleSendMessageAsync(message));
                    break;

                case MessageType.SendFile:
                    await RequireAuthAsync(() => HandleSendFileAsync(message));
                    break;

                case MessageType.GetHistory:
                    await RequireAuthAsync(() => HandleGetHistoryAsync(message));
                    break;

                case MessageType.GetConversations:
                    await RequireAuthAsync(() => HandleGetConversationsAsync());
                    break;

                case MessageType.CreateConversation:
                    await RequireAuthAsync(() => HandleCreateConversationAsync(message));
                    break;

                case MessageType.GetOnlineUsers:
                    await RequireAuthAsync(() => HandleGetOnlineUsersAsync());
                    break;

                case MessageType.Ping:
                    // Phản hồi Ping để duy trì kết nối (heartbeat)
                    await _connection.SendAsync(new NetworkMessage
                    {
                        Type = MessageType.Pong,
                        Timestamp = DateTime.UtcNow,
                    });
                    break;

                default:
                    await _connection.SendAsync(
                        NetworkMessage.CreateError(
                            $"Unknown message type: {message.Type}"));
                    break;
            }
        }

        // ── Xác thực ──────────────────────────────────────────────

        private async Task HandleLoginAsync(NetworkMessage message)
        {
            var response = await _authService.LoginAsync(message.Payload);
            await _connection.SendAsync(response);

            // Nếu login thành công, cập nhật trạng thái kết nối và thông báo online
            if (response.Type == MessageType.AuthResponse &&
                response.SenderId.HasValue)
            {
                _connection.SetAuthenticated(
                    response.SenderId.Value,
                    response.SenderName ?? "Unknown");

                await _authService.SetUserOnlineAsync(response.SenderId.Value);

                // Thông báo cho tất cả Client khác rằng người dùng này online
                await _server.BroadcastToAllExceptAsync(
                    new NetworkMessage
                    {
                        Type = MessageType.UserOnline,
                        SenderId = response.SenderId,
                        SenderName = response.SenderName,
                        Timestamp = DateTime.UtcNow,
                    },
                    excludeConnectionId: _connection.ConnectionId);

                Console.WriteLine(
                    $"[*] User '{response.SenderName}' logged in " +
                    $"[{_connection.ConnectionId[..8]}]");
            }
        }

        private async Task HandleRegisterAsync(NetworkMessage message)
        {
            var response = await _authService.RegisterAsync(message.Payload);
            await _connection.SendAsync(response);

            if (response.Type == MessageType.AuthResponse &&
                response.SenderId.HasValue)
            {
                _connection.SetAuthenticated(
                    response.SenderId.Value,
                    response.SenderName ?? "Unknown");

                Console.WriteLine(
                    $"[*] User '{response.SenderName}' registered " +
                    $"[{_connection.ConnectionId[..8]}]");
            }
        }

        // ── Tin nhắn ──────────────────────────────────────────────

        private async Task HandleSendMessageAsync(NetworkMessage message)
        {
            // Lưu tin nhắn vào DB và nhận gói BroadcastMessage
            var broadcast = await _messageService.SaveAndBroadcastAsync(
                _connection.AuthenticatedUserId!.Value,
                _connection.DisplayName!,
                message);

            if (broadcast.Type == MessageType.Error)
            {
                await _connection.SendAsync(broadcast);
                return;
            }

            // Broadcast tin nhắn đến tất cả thành viên trong cuộc hội thoại
            await _server.BroadcastToConversationAsync(
                broadcast,
                broadcast.ConversationId!.Value);
        }

        private async Task HandleSendFileAsync(NetworkMessage message)
        {
            var broadcast = await _fileService.SaveAndBroadcastFileAsync(
                _connection.AuthenticatedUserId!.Value,
                _connection.DisplayName!,
                message);

            if (broadcast.Type == MessageType.Error)
            {
                await _connection.SendAsync(broadcast);
                return;
            }

            await _server.BroadcastToConversationAsync(
                broadcast,
                broadcast.ConversationId!.Value);
        }

        private async Task HandleGetHistoryAsync(NetworkMessage message)
        {
            var response = await _messageService.GetHistoryAsync(
                _connection.AuthenticatedUserId!.Value,
                message.Payload);

            await _connection.SendAsync(response);
        }

        private async Task HandleGetConversationsAsync()
        {
            var response = await _messageService.GetConversationsAsync(
                _connection.AuthenticatedUserId!.Value);

            await _connection.SendAsync(response);
        }

        private async Task HandleCreateConversationAsync(NetworkMessage message)
        {
            var response = await _messageService.CreateConversationAsync(
                _connection.AuthenticatedUserId!.Value,
                message.Payload);

            await _connection.SendAsync(response);
        }

        // ── Người dùng online ─────────────────────────────────────

        private async Task HandleGetOnlineUsersAsync()
        {
            // Lấy danh sách kết nối đã xác thực từ Server
            var onlineUsers = _server.GetOnlineUsers();

            var responsePayload = new { users = onlineUsers };

            await _connection.SendAsync(new NetworkMessage
            {
                Type = MessageType.OnlineUsersResponse,
                Payload = JsonSerializer.SerializeToElement(responsePayload),
                Timestamp = DateTime.UtcNow,
            });
        }

        // ── Tiện ích ──────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra Client đã xác thực chưa trước khi thực hiện hành động.
        /// Gửi gói Error nếu chưa xác thực.
        /// </summary>
        private async Task RequireAuthAsync(Func<Task> action)
        {
            if (!_connection.IsAuthenticated)
            {
                await _connection.SendAsync(
                    NetworkMessage.CreateError(
                        "Authentication required. Please login first."));
                return;
            }

            await action();
        }

        /// <summary>
        /// Dọn dẹp tài nguyên khi Client ngắt kết nối:
        ///   - Cập nhật trạng thái Offline trong DB (nếu đã login).
        ///   - Xoá khỏi danh sách kết nối của Server.
        ///   - Thông báo Offline đến các Client khác.
        /// </summary>
        private async Task CleanupAsync()
        {
            if (_connection.IsAuthenticated)
            {
                await _authService.SetUserOfflineAsync(
                    _connection.AuthenticatedUserId!.Value);

                // Thông báo cho các Client khác rằng người dùng này offline
                await _server.BroadcastToAllExceptAsync(
                    new NetworkMessage
                    {
                        Type = MessageType.UserOffline,
                        SenderId = _connection.AuthenticatedUserId,
                        SenderName = _connection.DisplayName,
                        Timestamp = DateTime.UtcNow,
                    },
                    excludeConnectionId: _connection.ConnectionId);

                Console.WriteLine(
                    $"[-] User '{_connection.DisplayName}' disconnected " +
                    $"[{_connection.ConnectionId[..8]}]");
            }
            else
            {
                Console.WriteLine(
                    $"[-] Anonymous client disconnected " +
                    $"[{_connection.ConnectionId[..8]}]");
            }

            _server.RemoveConnection(_connection.ConnectionId);
            _connection.Dispose();
        }
    }
}
