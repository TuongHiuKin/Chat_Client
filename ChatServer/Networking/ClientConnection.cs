using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Protocol;
using ChatServer.Data;
using ChatServer.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Networking
{
    /// <summary>
    /// Đại diện cho một kết nối TCP của một Client đang kết nối vào Server.
    /// Mỗi ClientConnection quản lý vòng đời đầy đủ của một Client:
    ///   - Đọc và gửi gói NetworkMessage qua NetworkStream.
    ///   - Theo dõi trạng thái xác thực (đã login chưa, UserId, DisplayName).
    ///   - Giải phóng tài nguyên khi ngắt kết nối.
    /// </summary>
    public class ClientConnection : IDisposable
    {
        // ── Trạng thái kết nối ────────────────────────────────────
        /// <summary>ID định danh duy nhất của kết nối này (GUID).</summary>
        public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>UserId sau khi Client đã xác thực thành công. Null nếu chưa login.</summary>
        public int? AuthenticatedUserId { get; private set; }

        /// <summary>DisplayName của người dùng sau khi xác thực.</summary>
        public string? DisplayName { get; private set; }

        /// <summary>Cho biết Client đã xác thực thành công chưa.</summary>
        public bool IsAuthenticated => AuthenticatedUserId.HasValue;

        /// <summary>Địa chỉ IP:Port của Client từ xa.</summary>
        public string RemoteEndpoint =>
            _tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";

        // ── Tài nguyên mạng ───────────────────────────────────────
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _disposed;

        public ClientConnection(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            // Dùng StreamReader để đọc từng dòng (mỗi NetworkMessage kết thúc bằng '\n')
            _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        }

        // ── Xác thực ──────────────────────────────────────────────

        /// <summary>Cập nhật thông tin người dùng sau khi xác thực thành công.</summary>
        public void SetAuthenticated(int userId, string displayName)
        {
            AuthenticatedUserId = userId;
            DisplayName = displayName;
        }

        // ── Đọc / Gửi gói tin ─────────────────────────────────────

        /// <summary>
        /// Đọc một dòng JSON từ stream, phân tích thành NetworkMessage.
        /// Trả về null nếu Client đã ngắt kết nối hoặc gói tin không hợp lệ.
        /// </summary>
        public async Task<NetworkMessage?> ReadMessageAsync(
            CancellationToken ct = default)
        {
            string? line = await _reader.ReadLineAsync(ct);
            if (line is null) return null; // Client đóng kết nối

            return NetworkMessage.Deserialize(line);
        }

        /// <summary>
        /// Gửi một NetworkMessage đến Client theo định dạng JSON + '\n'.
        /// Thread-safe: dùng SemaphoreSlim để tránh ghi đồng thời.
        /// </summary>
        public async Task SendAsync(
            NetworkMessage message,
            CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                string json = message.Serialize(); // Có sẵn ký tự '\n' ở cuối
                byte[] data = Encoding.UTF8.GetBytes(json);
                await _stream.WriteAsync(data, ct);
                await _stream.FlushAsync(ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ── Giải phóng tài nguyên ─────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _writeLock.Dispose();
            _reader.Dispose();
            _stream.Dispose();
            _tcpClient.Dispose();
        }
    }
}
