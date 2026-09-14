using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Models
{
    public class Message
    {
        public long MessageId { get; set; }

        public int ConversationId { get; set; }

        public int SenderId { get; set; }

        public string MessageType { get; set; }
            = string.Empty;

        public string? Content { get; set; }

        public DateTime SentAt { get; set; }

        public bool IsDeleted { get; set; }

        public Conversation Conversation { get; set; }
            = null!;

        public User Sender { get; set; }
            = null!;

        public ICollection<Attachment> Attachments { get; set; }
            = new List<Attachment>();
    }
}
