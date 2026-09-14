using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Models
{
    public class Conversation
    {
        public int ConversationId { get; set; }

        public string ConversationType { get; set; }
            = string.Empty;

        public string? ConversationName { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<ConversationMember> Members { get; set; }
            = new List<ConversationMember>();

        public ICollection<Message> Messages { get; set; }
            = new List<Message>();
    }
}
