using ChatServer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChatServer.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(
            DbContextOptions<ChatDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Conversation> Conversations
            => Set<Conversation>();

        public DbSet<ConversationMember>
            ConversationMembers => Set<ConversationMember>();

        public DbSet<Message> Messages
            => Set<Message>();

        public DbSet<Attachment> Attachments
            => Set<Attachment>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasKey(x => x.UserId);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username)
                .IsUnique();

            modelBuilder.Entity<Conversation>()
                .HasKey(x => x.ConversationId);

            modelBuilder.Entity<ConversationMember>()
                .HasKey(x => new
                {
                    x.ConversationId,
                    x.UserId
                });

            modelBuilder.Entity<ConversationMember>()
                .HasOne(x => x.Conversation)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.ConversationId);

            modelBuilder.Entity<ConversationMember>()
                .HasOne(x => x.User)
                .WithMany(x => x.ConversationMembers)
                .HasForeignKey(x => x.UserId);

            modelBuilder.Entity<Message>()
                .HasKey(x => x.MessageId);

            modelBuilder.Entity<Message>()
                .HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId);

            modelBuilder.Entity<Message>()
                .HasOne(x => x.Sender)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SenderId);

            modelBuilder.Entity<Attachment>()
                .HasKey(x => x.AttachmentId);

            modelBuilder.Entity<Attachment>()
                .HasOne(x => x.Message)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MessageId);
        }
    }
}
