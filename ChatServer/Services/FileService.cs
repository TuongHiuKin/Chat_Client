using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Models;
using ChatServer.Protocol;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services
{
    /// <summary>
    /// Dịch vụ quản lý gửi nhận và lưu trữ tập tin/hình ảnh trong ChatServer.
    /// File được lưu trữ vào bảng Attachments trong CSDL SQL Server và phát tán (broadcast) đến các Client.
    /// </summary>
    public class FileService
    {
        private readonly ChatDbContext _db;

        // Giới hạn dung lượng tối đa 10 MB cho mỗi file
        private const int MaxFileSizeBytes = 10 * 1024 * 1024;

        public FileService(ChatDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Xử lý gói tin SendFile: Kiểm tra tính hợp lệ, lưu vào DB và trả về BroadcastMessage.
        /// </summary>
        public async Task<NetworkMessage> SaveAndBroadcastFileAsync(
            int senderId,
            string senderName,
            NetworkMessage incomingMessage)
        {
            if (incomingMessage.ConversationId is null)
                return NetworkMessage.CreateError("ConversationId is required.");

            if (incomingMessage.Payload is null)
                return NetworkMessage.CreateError("SendFile payload is missing.");

            int conversationId = incomingMessage.ConversationId.Value;

            // Kiểm tra người gửi có trong cuộc hội thoại không
            bool isMember = await _db.ConversationMembers
                .AnyAsync(m => m.ConversationId == conversationId && m.UserId == senderId);

            if (!isMember)
                return NetworkMessage.CreateError("You are not a member of this conversation.");

            var payload = incomingMessage.Payload.Value;

            string fileName = payload.TryGetProperty("fileName", out var fn)
                ? (fn.GetString() ?? "file") : "file";
            string contentType = payload.TryGetProperty("contentType", out var ct)
                ? (ct.GetString() ?? "application/octet-stream") : "application/octet-stream";
            string messageType = payload.TryGetProperty("messageType", out var mt)
                ? (mt.GetString() ?? "file") : "file";
            string? dataBase64 = payload.TryGetProperty("dataBase64", out var db64)
                ? db64.GetString() : null;

            if (string.IsNullOrWhiteSpace(dataBase64))
                return NetworkMessage.CreateError("File data cannot be empty.");

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(dataBase64);
            }
            catch
            {
                return NetworkMessage.CreateError("Invalid Base64 file data.");
            }

            if (fileBytes.Length > MaxFileSizeBytes)
                return NetworkMessage.CreateError("File size exceeds 10 MB limit.");

            // Chuẩn hóa messageType: nếu định dạng ảnh thì đặt là image
            if (messageType != "image" && IsImageExtension(Path.GetExtension(fileName)))
            {
                messageType = "image";
            }

            // Tạo tin nhắn mới
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                MessageType = messageType,
                Content = fileName,
                SentAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            // Tạo bản ghi Attachment
            var attachment = new Attachment
            {
                MessageId = message.MessageId,
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileBytes.Length,
                FileData = fileBytes,
            };

            _db.Attachments.Add(attachment);
            await _db.SaveChangesAsync();

            // Broadcast gói tin về cho các Client
            var broadcastPayload = new
            {
                messageId = message.MessageId,
                messageType = message.MessageType,
                attachmentId = attachment.AttachmentId,
                fileName = attachment.FileName,
                fileSize = attachment.FileSize,
                contentType = attachment.ContentType,
                dataBase64 = dataBase64,
            };

            return new NetworkMessage
            {
                Type = MessageType.BroadcastMessage,
                SenderId = senderId,
                SenderName = senderName,
                ConversationId = conversationId,
                Content = fileName,
                Timestamp = message.SentAt,
                Payload = JsonSerializer.SerializeToElement(broadcastPayload),
            };
        }

        private static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
        }
    }
}
