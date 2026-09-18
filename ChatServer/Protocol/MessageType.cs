using System;

namespace ChatServer.Protocol
{
    /// <summary>
    /// Định nghĩa các loại gói tin trao đổi giữa Server và Client.
    /// Mỗi giá trị tương ứng với một hành động / sự kiện cụ thể.
    /// </summary>
    public enum MessageType
    {
        // ── Xác thực ──────────────────────────────────────────────
        /// <summary>Client gửi yêu cầu đăng nhập lên Server.</summary>
        Login = 1,

        /// <summary>Client gửi yêu cầu đăng ký tài khoản mới.</summary>
        Register = 2,

        /// <summary>Server phản hồi kết quả xác thực (thành công / thất bại).</summary>
        AuthResponse = 3,

        // ── Tin nhắn ──────────────────────────────────────────────
        /// <summary>Client gửi tin nhắn text đến một cuộc hội thoại cụ thể.</summary>
        SendMessage = 10,

        /// <summary>Server phân phối tin nhắn đến tất cả thành viên trong cuộc hội thoại.</summary>
        BroadcastMessage = 11,

        /// <summary>Client yêu cầu tải lịch sử tin nhắn của một cuộc hội thoại.</summary>
        GetHistory = 12,

        /// <summary>Server phản hồi lịch sử tin nhắn.</summary>
        HistoryResponse = 13,

        /// <summary>Client gửi tập tin hoặc hình ảnh đính kèm.</summary>
        SendFile = 14,
        UploadStart = 40, UploadChunk = 41, UploadFinish = 42, UploadCancel = 43,
        DownloadChunk = 44, TransferResponse = 45, JoinConversation = 46, ConversationsChanged = 47,

        // ── Cuộc hội thoại ────────────────────────────────────────
        /// <summary>Client yêu cầu danh sách các cuộc hội thoại của mình.</summary>
        GetConversations = 20,

        /// <summary>Server phản hồi danh sách cuộc hội thoại.</summary>
        ConversationsResponse = 21,

        /// <summary>Client tạo cuộc hội thoại mới (1-1 hoặc nhóm).</summary>
        CreateConversation = 22,

        // ── Trạng thái người dùng ─────────────────────────────────
        /// <summary>Server thông báo có người dùng mới kết nối / online.</summary>
        UserOnline = 30,

        /// <summary>Server thông báo người dùng nào đó ngắt kết nối / offline.</summary>
        UserOffline = 31,

        /// <summary>Client yêu cầu danh sách người dùng đang online.</summary>
        GetOnlineUsers = 32,

        /// <summary>Server phản hồi danh sách người dùng đang online.</summary>
        OnlineUsersResponse = 33,

        // ── Hệ thống ──────────────────────────────────────────────
        /// <summary>Gói tin kiểm tra kết nối còn sống (heartbeat).</summary>
        Ping = 90,

        /// <summary>Phản hồi Ping để xác nhận kết nối.</summary>
        Pong = 91,

        /// <summary>Server thông báo lỗi về phía Client.</summary>
        Error = 99,
    }
}
