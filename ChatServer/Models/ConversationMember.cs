using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Models
{
    public class ConversationMember
    {
        public int ConversationId { get; set; }

        public int UserId { get; set; }

        public Conversation Conversation { get; set; }
            = null!;

        public User User { get; set; }
            = null!;
    }
}
