using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Protocol;
using ChatServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services
{
    /// <summary>
    /// Dịch vụ quản lý tin nhắn và cuộc hội thoại.
    /// Chịu trách nhiệm: Lưu tin nhắn, tải lịch sử, tạo cuộc hội thoại, quản lý thành viên.
    /// Mọi dữ liệu đều được persist vào SQL Server thông qua ChatDbContext.
    /// </summary>
    public class MessageService
    {
        private readonly ChatDbContext _db;

        // Số lượng tin nhắn tải mỗi lần (phân trang lịch sử)
        private const int HistoryPageSize = 50;

        public MessageService(ChatDbContext db)
        {
            _db = db;
        }

        // ── Lưu tin nhắn vào DB ────────────────────────────────────

        /// <summary>
        /// Lưu một tin nhắn mới vào bảng Messages trong DB.
        /// Trả về NetworkMessage dạng BroadcastMessage để phân phối tới Client khác.
        /// </summary>
        /// <param name="senderId">UserId của người gửi (đã xác thực).</param>
        /// <param name="senderName">DisplayName của người gửi.</param>
        /// <param name="incomingMessage">Gói tin gốc nhận từ Client (loại SendMessage).</param>
        /// <returns>NetworkMessage loại BroadcastMessage hoặc Error.</returns>
        public async Task<NetworkMessage> SaveAndBroadcastAsync(
            int senderId,
            string senderName,
            NetworkMessage incomingMessage)
        {
            // Kiểm tra hợp lệ tham số đầu vào
            if (incomingMessage.ConversationId is null)
                return NetworkMessage.CreateError("ConversationId is required.");

            if (string.IsNullOrWhiteSpace(incomingMessage.Content))
                return NetworkMessage.CreateError("Message content cannot be empty.");

            int conversationId = incomingMessage.ConversationId.Value;

            // Kiểm tra người gửi có phải thành viên cuộc hội thoại không
            bool isMember = await _db.ConversationMembers
                .AnyAsync(m => m.ConversationId == conversationId
                            && m.UserId == senderId);

            if (!isMember)
                return NetworkMessage.CreateError(
                    "You are not a member of this conversation.");

            // Tạo bản ghi tin nhắn mới và lưu vào DB
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                MessageType = "text",
                Content = incomingMessage.Content.Trim(),
                SentAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            // Trả về gói BroadcastMessage để Server phân phối cho các thành viên
            return new NetworkMessage
            {
                Type = MessageType.BroadcastMessage,
                SenderId = senderId,
                SenderName = senderName,
                ConversationId = conversationId,
                Content = message.Content,
                Timestamp = message.SentAt,
            };
        }

        // ── Lịch sử tin nhắn ──────────────────────────────────────

        /// <summary>
        /// Tải lịch sử tin nhắn của một cuộc hội thoại từ DB (phân trang theo offset).
        /// Chỉ trả về tin nhắn chưa bị xoá (IsDeleted = false).
        /// </summary>
        /// <param name="requesterId">UserId yêu cầu (phải là thành viên).</param>
        /// <param name="payload">JSON chứa {conversationId, offset} từ gói GetHistory.</param>
        /// <returns>NetworkMessage loại HistoryResponse chứa danh sách tin nhắn trong Payload.</returns>
        public async Task<NetworkMessage> GetHistoryAsync(
            int requesterId,
            JsonElement? payload)
        {
            if (payload is null)
                return NetworkMessage.CreateError("GetHistory payload is missing.");

            if (!payload.Value.TryGetProperty("conversationId", out var cidEl) ||
                !cidEl.TryGetInt32(out int conversationId))
                return NetworkMessage.CreateError("conversationId is required in payload.");

            int offset = 0;
            if (payload.Value.TryGetProperty("offset", out var offsetEl))
                offsetEl.TryGetInt32(out offset);

            // Kiểm tra quyền truy cập cuộc hội thoại
            bool isMember = await _db.ConversationMembers
                .AnyAsync(m => m.ConversationId == conversationId
                            && m.UserId == requesterId);

            if (!isMember)
                return NetworkMessage.CreateError(
                    "You are not a member of this conversation.");

            // Tải danh sách tin nhắn từ DB, sắp xếp mới nhất trước, phân trang
            var rawMessages = await _db.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip(offset)
                .Take(HistoryPageSize)
                .Include(m => m.Sender)
                .Include(m => m.Attachments)
                .ToListAsync();

            var messages = rawMessages.Select(m =>
            {
                var att = m.Attachments.FirstOrDefault();
                return new
                {
                    messageId = m.MessageId,
                    senderId = m.SenderId,
                    senderName = m.Sender?.DisplayName ?? "Unknown",
                    content = m.Content,
                    sentAt = m.SentAt,
                    messageType = m.MessageType,
                    attachment = att == null ? null : new
                    {
                        attachmentId = att.AttachmentId,
                        fileName = att.FileName,
                        fileSize = att.FileSize,
                        contentType = att.ContentType,
                        dataBase64 = att.FileData != null ? Convert.ToBase64String(att.FileData) : null,
                    }
                };
            }).ToList();

            var historyPayload = new
            {
                conversationId,
                offset,
                messages = messages.OrderBy(m => m.sentAt).ToList(),
                hasMore = messages.Count == HistoryPageSize,
            };

            return new NetworkMessage
            {
                Type = MessageType.HistoryResponse,
                ConversationId = conversationId,
                Payload = JsonSerializer.SerializeToElement(historyPayload),
                Timestamp = DateTime.UtcNow,
            };
        }

        // ── Cuộc hội thoại ────────────────────────────────────────

        /// <summary>
        /// Tải danh sách tất cả cuộc hội thoại mà người dùng là thành viên từ DB.
        /// </summary>
        /// <param name="userId">UserId của người dùng yêu cầu.</param>
        /// <returns>NetworkMessage loại ConversationsResponse chứa danh sách cuộc hội thoại.</returns>
        public async Task<NetworkMessage> GetConversationsAsync(int userId)
        {
            var conversations = await _db.ConversationMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .Include(m => m.Conversation)
                    .ThenInclude(c => c.Members)
                        .ThenInclude(cm => cm.User)
                .Select(m => new
                {
                    conversationId = m.ConversationId,
                    conversationType = m.Conversation.ConversationType,
                    conversationName = m.Conversation.ConversationName,
                    createdAt = m.Conversation.CreatedAt,
                    memberCount = m.Conversation.Members.Count,
                    members = m.Conversation.Members.Select(cm => new
                    {
                        userId = cm.UserId,
                        displayName = cm.User.DisplayName,
                        status = cm.User.Status,
                    }).ToList(),
                })
                .ToListAsync();

            var responsePayload = new { conversations };

            return new NetworkMessage
            {
                Type = MessageType.ConversationsResponse,
                SenderId = userId,
                Payload = JsonSerializer.SerializeToElement(responsePayload),
                Timestamp = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Tạo cuộc hội thoại mới (loại "direct" hoặc "group") và lưu vào DB.
        /// Tự động thêm người tạo và các thành viên khác vào ConversationMembers.
        /// </summary>
        /// <param name="creatorId">UserId của người tạo cuộc hội thoại.</param>
        /// <param name="payload">JSON chứa {type, name, memberIds[]} từ gói CreateConversation.</param>
        /// <returns>NetworkMessage phản hồi kết quả tạo cuộc hội thoại.</returns>
        public async Task<NetworkMessage> CreateConversationAsync(
            int creatorId,
            JsonElement? payload)
        {
            if (payload is null)
                return NetworkMessage.CreateError("CreateConversation payload is missing.");

            string conversationType = payload.Value.TryGetProperty("type", out var typeEl)
                ? (typeEl.GetString() ?? "direct") : "direct";

            string? conversationName = payload.Value.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() : null;

            // Lấy danh sách thành viên từ payload
            List<int> memberIds = new() { creatorId };
            if (payload.Value.TryGetProperty("memberIds", out var membersEl) &&
                membersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in membersEl.EnumerateArray())
                {
                    if (item.TryGetInt32(out int mid) && mid != creatorId)
                        memberIds.Add(mid);
                }
            }

            // Tạo cuộc hội thoại mới
            var conversation = new Conversation
            {
                ConversationType = conversationType,
                ConversationName = conversationName,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();

            // Thêm tất cả thành viên vào ConversationMembers
            foreach (int memberId in memberIds)
            {
                _db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = conversation.ConversationId,
                    UserId = memberId,
                });
            }

            await _db.SaveChangesAsync();

            var responsePayload = new
            {
                conversationId = conversation.ConversationId,
                conversationType,
                conversationName,
                memberIds,
            };

            return new NetworkMessage
            {
                Type = MessageType.ConversationsResponse,
                ConversationId = conversation.ConversationId,
                Payload = JsonSerializer.SerializeToElement(responsePayload),
                Timestamp = DateTime.UtcNow,
            };
        }
    }
}
