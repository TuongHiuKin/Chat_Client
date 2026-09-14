using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Protocol;
using ChatServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services
{
    /// <summary>
    /// Dịch vụ xác thực người dùng.
    /// Chịu trách nhiệm: Đăng nhập, Đăng ký và băm mật khẩu.
    /// Mọi thông tin người dùng được lưu trữ vào bảng Users trong SQL Server.
    /// </summary>
    public class AuthenticationService
    {
        private readonly ChatDbContext _db;

        public AuthenticationService(ChatDbContext db)
        {
            _db = db;
        }

        // ── Đăng nhập ─────────────────────────────────────────────

        /// <summary>
        /// Xử lý yêu cầu đăng nhập từ Client.
        /// Kiểm tra Username, so sánh PasswordHash và cập nhật LastLoginAt.
        /// </summary>
        /// <param name="payload">JSON chứa {username, password} từ gói Login.</param>
        /// <returns>NetworkMessage phản hồi: AuthResponse thành công hoặc thất bại.</returns>
        public async Task<NetworkMessage> LoginAsync(JsonElement? payload)
        {
            if (payload is null)
                return NetworkMessage.CreateError("Login payload is missing.");

            // Trích xuất thông tin đăng nhập từ payload JSON
            string? username = payload.Value.TryGetProperty("username", out var u)
                ? u.GetString() : null;
            string? password = payload.Value.TryGetProperty("password", out var p)
                ? p.GetString() : null;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
                return NetworkMessage.CreateError("Username and password are required.");

            // Truy vấn người dùng theo Username (phân biệt chữ hoa/thường tùy collation DB)
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Username == username);

            if (user is null)
                return AuthFailedResponse("Invalid username or password.");

            // Xác minh mật khẩu đã băm
            if (!VerifyPassword(password, user.PasswordHash))
                return AuthFailedResponse("Invalid username or password.");

            // Cập nhật thời điểm đăng nhập cuối cùng vào DB
            await UpdateLastLoginAsync(user.UserId);

            // Xây dựng payload phản hồi thành công
            var responsePayload = new
            {
                success = true,
                userId = user.UserId,
                username = user.Username,
                displayName = user.DisplayName,
                avatar = user.Avatar,
            };

            return new NetworkMessage
            {
                Type = MessageType.AuthResponse,
                SenderId = user.UserId,
                SenderName = user.DisplayName,
                Payload = JsonSerializer.SerializeToElement(responsePayload),
                Timestamp = DateTime.UtcNow,
            };
        }

        // ── Đăng ký ───────────────────────────────────────────────

        /// <summary>
        /// Xử lý yêu cầu đăng ký tài khoản mới từ Client.
        /// Kiểm tra Username đã tồn tại, tạo User mới và lưu vào DB.
        /// </summary>
        /// <param name="payload">JSON chứa {username, password, displayName} từ gói Register.</param>
        /// <returns>NetworkMessage phản hồi: AuthResponse thành công hoặc lỗi.</returns>
        public async Task<NetworkMessage> RegisterAsync(JsonElement? payload)
        {
            if (payload is null)
                return NetworkMessage.CreateError("Register payload is missing.");

            string? username = payload.Value.TryGetProperty("username", out var u)
                ? u.GetString() : null;
            string? password = payload.Value.TryGetProperty("password", out var p)
                ? p.GetString() : null;
            string? displayName = payload.Value.TryGetProperty("displayName", out var d)
                ? d.GetString() : null;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
                return NetworkMessage.CreateError("Username and password are required.");

            // Kiểm tra Username đã được đăng ký chưa
            bool exists = await _db.Users
                .AsNoTracking()
                .AnyAsync(x => x.Username == username);

            if (exists)
                return AuthFailedResponse($"Username '{username}' is already taken.");

            // Tạo bản ghi người dùng mới với mật khẩu được băm
            var newUser = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? username
                    : displayName,
                Status = "Online",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            var responsePayload = new
            {
                success = true,
                userId = newUser.UserId,
                username = newUser.Username,
                displayName = newUser.DisplayName,
            };

            return new NetworkMessage
            {
                Type = MessageType.AuthResponse,
                SenderId = newUser.UserId,
                SenderName = newUser.DisplayName,
                Payload = JsonSerializer.SerializeToElement(responsePayload),
                Timestamp = DateTime.UtcNow,
            };
        }

        // ── Quản lý trạng thái online ─────────────────────────────

        /// <summary>Cập nhật trạng thái người dùng thành Online trong DB.</summary>
        public async Task SetUserOnlineAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return;
            user.Status = "Online";
            await _db.SaveChangesAsync();
        }

        /// <summary>Cập nhật trạng thái người dùng thành Offline trong DB.</summary>
        public async Task SetUserOfflineAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return;
            user.Status = "Offline";
            await _db.SaveChangesAsync();
        }

        // ── Băm mật khẩu (SHA-256 + Salt) ─────────────────────────

        /// <summary>
        /// Băm mật khẩu thuần tuý thành chuỗi lưu DB.
        /// Sử dụng SHA-256 với salt ngẫu nhiên theo định dạng: {salt}:{hash}.
        /// </summary>
        private static string HashPassword(string password)
        {
            // Sinh salt ngẫu nhiên 16 bytes
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            string salt = Convert.ToBase64String(saltBytes);

            // Băm (salt + password) bằng SHA-256
            byte[] hashBytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(salt + password));
            string hash = Convert.ToBase64String(hashBytes);

            return $"{salt}:{hash}";
        }

        /// <summary>Xác minh mật khẩu thuần tuý so với chuỗi băm đã lưu trong DB.</summary>
        private static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var parts = storedHash.Split(':', 2);
                if (parts.Length != 2) return false;

                string salt = parts[0];
                string expectedHash = parts[1];

                byte[] hashBytes = SHA256.HashData(
                    Encoding.UTF8.GetBytes(salt + password));
                string actualHash = Convert.ToBase64String(hashBytes);

                // Dùng so sánh an toàn (constant-time) để tránh timing attack
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(actualHash),
                    Encoding.UTF8.GetBytes(expectedHash));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Cập nhật thời gian đăng nhập cuối cùng của người dùng.</summary>
        private async Task UpdateLastLoginAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return;
            user.LastLoginAt = DateTime.UtcNow;
            user.Status = "Online";
            await _db.SaveChangesAsync();
        }
        /// <summary>
        /// Tạo gói AuthResponse thất bại với thông báo lỗi cụ thể.
        /// Dùng MessageType.AuthResponse thay vì Error để Client xử lý
        /// đúng luồng xác thực (hide loading, re-enable button).
        /// </summary>
        private static NetworkMessage AuthFailedResponse(string errorMessage) =>
            new NetworkMessage
            {
                Type = MessageType.AuthResponse,
                Payload = System.Text.Json.JsonSerializer.SerializeToElement(
                    new { success = false, errorMessage }),
                Timestamp = DateTime.UtcNow,
            };
    }
}
