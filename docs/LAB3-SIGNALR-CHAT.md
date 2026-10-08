# LAB 3 — Real-Time Online Chat with ASP.NET Core SignalR

## 1. Lab Objective

The objective of Lab 3 is to implement a lightweight, real-time web-based online chat application using **ASP.NET Core SignalR**, **MVC**, **JavaScript**, and **Bootstrap**. 
This lab provides foundational practice in bidirectional, persistent, real-time web socket communication before building more advanced features in Assignment 2.

---

## 2. Project Structure

```text
Lab3/
├── Controllers/
│   ├── ChatController.cs          # Returns the Chat view
│   └── HomeController.cs          # Default controller
├── Hubs/
│   └── ChatHub.cs                 # SignalR hub handling connections, join/leave, and broadcasts
├── Views/
│   ├── Chat/
│   │   └── Index.cshtml           # Responsive, accessible chat UI
│   └── Shared/
│       └── _Layout.cshtml         # Common navbar layout
├── wwwroot/
│   ├── js/
│   │   └── chat.js                # SignalR HubConnectionBuilder, DOM events, and safe text rendering
│   └── lib/
│       ├── bootstrap/             # Bootstrap CSS & JS
│       └── microsoft/signalr/     # SignalR JavaScript client library
├── Program.cs                     # SignalR service registration and endpoint mapping
└── Lab3.csproj                    # Target .NET 8 (ASP.NET Core)
```

---

## 3. SignalR Request & Event Flow

### Communication Diagram:

```text
Browser A
    \
     \
       ChatHub ────► Browser A
     /          ────► Browser B
    /           ────► Browser C
Browser B
```

### Event Lifecycle:

1. **Connection Initialization:**
   * Browser loads `chat.js` and creates `new signalR.HubConnectionBuilder().withUrl("/chatHub").build()`.
   * On successful handshake, client receives `UpdateUserCount` from server and enables UI controls.
2. **User Joins (`JoinChat`):**
   * User inputs a display name (e.g. `Kien`) and clicks **Set Name / Join** (or presses Enter).
   * Client calls `connection.invoke("JoinChat", user)`.
   * `ChatHub` registers `ConnectionId -> Username` in a thread-safe `ConcurrentDictionary`.
   * `ChatHub` broadcasts event `UserJoined(user, timestamp)` and `UpdateUserCount(count)` to all clients.
3. **Sending Message (`SendMessage`):**
   * User enters message and presses Enter or clicks **Send**.
   * Client validates that user name and message are non-empty and under 500 characters.
   * Client invokes `connection.invoke("SendMessage", user, message)`.
   * Server validates constraints and broadcasts `ReceiveMessage(user, message, timestamp)` to `Clients.All`.
   * Each connected browser immediately appends the message without page reload.
4. **User Leaves (`OnDisconnectedAsync`):**
   * When a browser window closes or navigates away, SignalR triggers `OnDisconnectedAsync`.
   * `ChatHub` removes the user and broadcasts `UserLeft(user, timestamp)` and `UpdateUserCount(count)` to remaining clients.

---

## 4. How to Run the Application

Open a Terminal / PowerShell window at `ChatSystem/Lab3`:

```powershell
cd D:\School\PRN222\ChatSystem\Lab3
dotnet run
```

The application will start on:
* **HTTP:** `http://localhost:5038`
* **HTTPS:** `https://localhost:7216`

---

## 5. How to Test with Two Browser Windows

1. **Launch the application:**
   Run `dotnet run` from `Lab3/`.
2. **Open Browser A:**
   Navigate to `http://localhost:5038/` in your primary browser window.
   * In **Display Name**, enter `Kien` and click **Set Name / Join**.
   * Notice system notification: `[HH:mm:ss] 👋 Kien joined the chat.`
3. **Open Browser B (or Incognito / Private Window):**
   Navigate to `http://localhost:5038/` in a second window.
   * In **Display Name**, enter `Vy` and click **Set Name / Join**.
   * Notice system notification in both windows: `[HH:mm:ss] 👋 Vy joined the chat.`
   * Notice **Users online** counter updates to `2`.
4. **Send Message from Browser A:**
   * In Browser A, type: `Hello everyone` and press **Enter** (or click **Send**).
   * **Verification:** Browser A displays `Kien (You): Hello everyone` with local timestamp.
   * **Verification:** Browser B immediately displays `Kien: Hello everyone` **without any page refresh**.
5. **Send Reply from Browser B:**
   * In Browser B, type: `Hi Kien!` and press **Enter**.
   * **Verification:** Both Browser A and Browser B receive the reply immediately in real time.
6. **Test Disconnection:**
   * Close Browser B.
   * **Verification:** Browser A immediately displays `[HH:mm:ss] 🚪 Vy left the chat.` and user count decreases to `1`.
7. **Test Validation:**
   * Try clicking **Send** with an empty message $\rightarrow$ Alert: *"Message cannot be empty."*
   * Try sending a message longer than 500 characters $\rightarrow$ Alert: *"Message cannot exceed 500 characters."*

---

## 6. Implemented Features

* ✅ **Real-Time Bidirectional Messaging:** Instant broadcast via SignalR WebSocket / Long Polling fallback.
* ✅ **No Page Refresh:** Complete dynamic DOM updates without page reloading.
* ✅ **Display Name Session:** Display name configured per session with quick join action.
* ✅ **Timestamp Display:** Every chat message and system notification displays formatted local time (`HH:mm:ss`).
* ✅ **Join Notification:** Immediate broadcast when a new participant enters the chat.
* ✅ **Leave Notification:** Immediate broadcast when a participant disconnects.
* ✅ **Empty Message Prevention:** Both client-side and server-side checks reject empty or whitespace-only messages.
* ✅ **Input Constraints & Length Limits:** Max 500 characters message limit with live character counter.
* ✅ **Safe Text Rendering (XSS Protection):** Strict usage of `textContent` ensures HTML/scripts in messages are rendered as plain text.
* ✅ **Keyboard Accessibility:** Enter key submits display name and sends messages seamlessly.
* ✅ **Scrollable Chat Container:** Fixed-height message container with automatic auto-scroll to the newest message.

---

## 7. Optional Features Implemented

* 👥 **Connected User Counter:** Real-time badge in header displaying active participants (`Users online: X`).
* 🎨 **Visual Distinction for Own Messages:** Sender's own messages are highlighted in primary blue with a `(You)` badge and right-aligned, while other messages are neutral and left-aligned.
* 🔄 **Connection Status Badge:** Dynamic badge showing `Connecting...`, `Connected`, `Reconnecting...`, or `Disconnected`.
* 🧹 **Clear Local Messages:** Utility button to clear local message view without affecting other participants.
