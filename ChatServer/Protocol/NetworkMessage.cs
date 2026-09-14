using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatServer.Protocol
{
    /// <summary>
    /// Cấu trúc gói tin chuẩn cho mọi giao tiếp giữa Server và Client.
    /// Được tuần tự hoá / giải tuần tự hoá bằng JSON (UTF-8).
    /// Mỗi gói tin kết thúc bằng ký tự '\n' để phân tách trên stream.
    /// </summary>
    public class NetworkMessage
    {
        // ── Phần định danh gói tin ───────────────────────────────
        /// <summary>Loại gói tin, xác định hành động cần thực hiện.</summary>
        [JsonPropertyName("type")]
        public MessageType Type { get; set; }

        /// <summary>
        /// ID định danh người gửi (UserId từ bảng Users).
        /// Null nếu chưa xác thực (ví dụ: gói Login/Register).
        /// </summary>
        [JsonPropertyName("senderId")]
        public int? SenderId { get; set; }

        /// <summary>Tên hiển thị của người gửi (dùng để hiển thị nhanh trên Client).</summary>
        [JsonPropertyName("senderName")]
        public string? SenderName { get; set; }

        // ── Phần mục tiêu ────────────────────────────────────────
        /// <summary>
        /// ID cuộc hội thoại liên quan.
        /// Null với các gói tin không gắn với cuộc hội thoại cụ thể.
        /// </summary>
        [JsonPropertyName("conversationId")]
        public int? ConversationId { get; set; }

        // ── Phần nội dung ────────────────────────────────────────
        /// <summary>Nội dung text của tin nhắn (với gói SendMessage / BroadcastMessage).</summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /// <summary>
        /// Payload JSON tổng quát dùng cho các gói tin phức tạp.
        /// Ví dụ: Login payload chứa {username, password},
        ///         AuthResponse chứa {success, userId, displayName, token, errorMessage}.
        /// </summary>
        [JsonPropertyName("payload")]
        public JsonElement? Payload { get; set; }

        // ── Phần thời gian ───────────────────────────────────────
        /// <summary>Thời điểm gói tin được tạo ra (UTC). Dùng để đồng bộ timestamp.</summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // ── Tiện ích tuần tự hoá ─────────────────────────────────

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        /// <summary>Chuyển đối tượng thành chuỗi JSON kết thúc bằng '\n'.</summary>
        public string Serialize() =>
            JsonSerializer.Serialize(this, _options) + "\n";

        /// <summary>Phân tích chuỗi JSON thành NetworkMessage. Trả về null nếu không hợp lệ.</summary>
        public static NetworkMessage? Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<NetworkMessage>(json, _options);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Tạo nhanh một gói tin lỗi để gửi về Client.</summary>
        public static NetworkMessage CreateError(string errorMessage) => new()
        {
            Type = MessageType.Error,
            Content = errorMessage,
            Timestamp = DateTime.UtcNow,
        };
    }
}
