using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? Avatar { get; set; }

        public string Status { get; set; } = "Offline";

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public ICollection<Message> Messages { get; set; }
            = new List<Message>();

        public ICollection<ConversationMember>
            ConversationMembers
        { get; set; }
            = new List<ConversationMember>();
    }
}
