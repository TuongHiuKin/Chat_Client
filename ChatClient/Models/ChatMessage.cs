using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ChatClient.Models
{
    /// <summary>
    /// Đại diện một tin nhắn hiển thị trong danh sách hội thoại ở giao diện Client.
    /// Hỗ trợ tin nhắn văn bản, tin nhắn hệ thống, hình ảnh và tập tin đính kèm.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>ID của tin nhắn (đồng bộ với MessageId bên Server/DB).</summary>
        public long? MessageId { get; set; }

        /// <summary>ID người gửi (UserId).</summary>
        public int? SenderId { get; set; }

        /// <summary>Tên hiển thị của người gửi.</summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>Nội dung tin nhắn (văn bản hoặc tên file).</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Thời điểm tin nhắn được gửi (UTC).</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>True nếu tin nhắn do người dùng hiện tại gửi đi.</summary>
        public bool IsOutgoing { get; set; }

        /// <summary>True nếu là thông báo hệ thống (vào/ra phòng...).</summary>
        public bool IsSystemMessage { get; set; }

        /// <summary>Loại tin nhắn: "text", "image", hoặc "file".</summary>
        public string MessageType { get; set; } = "text";

        public bool IsTextMessage => MessageType == "text" && !IsSystemMessage;
        public bool IsImageMessage => MessageType == "image";
        public bool IsFileMessage => MessageType == "file";

        /// <summary>Tên tập tin hoặc hình ảnh.</summary>
        public string? FileName { get; set; }

        /// <summary>Kích thước tập tin (bytes).</summary>
        public long? FileSize { get; set; }

        /// <summary>Dữ liệu nhị phân của tập tin dùng để lưu/tải về.</summary>
        public byte[]? RawFileData { get; set; }

        /// <summary>Hình ảnh hiển thị trực tiếp trong bubble chat.</summary>
        public BitmapSource? ImageSource { get; set; }

        /// <summary>Định dạng dung lượng file dễ đọc (ví dụ: 250 KB, 1.4 MB).</summary>
        public string FileSizeFormatted
        {
            get
            {
                if (!FileSize.HasValue || FileSize.Value <= 0) return string.Empty;
                double bytes = FileSize.Value;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
                return $"{bytes / (1024 * 1024):F1} MB";
            }
        }

        /// <summary>Thời gian hiển thị thân thiện theo múi giờ địa phương.</summary>
        public string DisplayTime =>
            Timestamp.ToLocalTime().ToString("HH:mm");

        /// <summary>Ngày hiển thị dùng để nhóm tin nhắn theo ngày.</summary>
        public string DisplayDate =>
            Timestamp.ToLocalTime().ToString("dd/MM/yyyy");

        /// <summary>
        /// Giải mã mảng byte thành BitmapSource an toàn cho luồng UI của WPF.
        /// </summary>
        public static BitmapSource? LoadBitmapFromBytes(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // Cần freeze để an toàn đa luồng trong WPF
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
