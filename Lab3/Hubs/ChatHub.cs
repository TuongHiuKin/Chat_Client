using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Lab3.Hubs
{
    public class ChatHub : Hub
    {
        // Tracks active connections and their display names (ConnectionId -> Username)
        private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new();

        /// <summary>
        /// Explicitly joins the chat with a display name after SignalR connection is established.
        /// </summary>
        public async Task JoinChat(string user)
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                await Clients.Caller.SendAsync("ValidationError", "Display name cannot be empty.");
                return;
            }

            var trimmedUser = user.Trim();
            if (trimmedUser.Length > 50)
            {
                trimmedUser = trimmedUser.Substring(0, 50);
            }

            ConnectedUsers[Context.ConnectionId] = trimmedUser;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            // Broadcast join notification to all connected clients
            await Clients.All.SendAsync("UserJoined", trimmedUser, timestamp);

            // Broadcast updated active user count
            await Clients.All.SendAsync("UpdateUserCount", ConnectedUsers.Count);
        }

        /// <summary>
        /// Sends a real-time message to all connected clients.
        /// </summary>
        public async Task SendMessage(string user, string message)
        {
            // Validation: user cannot be empty
            if (string.IsNullOrWhiteSpace(user))
            {
                await Clients.Caller.SendAsync("ValidationError", "Display name is required.");
                return;
            }

            // Validation: message cannot be empty or whitespace only
            if (string.IsNullOrWhiteSpace(message))
            {
                await Clients.Caller.SendAsync("ValidationError", "Message cannot be empty.");
                return;
            }

            var trimmedMessage = message.Trim();

            // Validation: maximum 500 characters
            if (trimmedMessage.Length > 500)
            {
                await Clients.Caller.SendAsync("ValidationError", "Message cannot exceed 500 characters.");
                return;
            }

            var trimmedUser = user.Trim();
            ConnectedUsers[Context.ConnectionId] = trimmedUser;

            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            // Broadcast message to all clients immediately
            await Clients.All.SendAsync("ReceiveMessage", trimmedUser, trimmedMessage, timestamp);
        }

        /// <summary>
        /// Triggered when a new client connects to the Hub.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // Provide the current user count to the caller
            await Clients.Caller.SendAsync("UpdateUserCount", ConnectedUsers.Count);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Triggered when a client disconnects from the Hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (ConnectedUsers.TryRemove(Context.ConnectionId, out var username))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                // Broadcast leave notification to all remaining clients
                await Clients.All.SendAsync("UserLeft", username, timestamp);
                await Clients.All.SendAsync("UpdateUserCount", ConnectedUsers.Count);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
