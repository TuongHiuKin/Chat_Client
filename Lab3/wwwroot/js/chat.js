"use strict";

// ── DOM Element References ────────────────────────────────────────────────────
const userInput = document.getElementById("userInput");
const joinButton = document.getElementById("joinButton");
const messageInput = document.getElementById("messageInput");
const sendButton = document.getElementById("sendButton");
const messagesContainer = document.getElementById("messagesContainer");
const messagesList = document.getElementById("messagesList");
const connectionBadge = document.getElementById("connectionBadge");
const userCountBadge = document.getElementById("userCountBadge");
const validationAlert = document.getElementById("validationAlert");
const charCount = document.getElementById("charCount");
const clearChatButton = document.getElementById("clearChatButton");

// ── State ────────────────────────────────────────────────────────────────────
let currentUsername = "";
let hasJoined = false;

// ── 1. Establish SignalR Connection ──────────────────────────────────────────
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

// ── Helper: Show / Hide Validation Alerts ────────────────────────────────────
function showError(message) {
    if (!validationAlert) return;
    validationAlert.textContent = message;
    validationAlert.classList.remove("d-none");
}

function clearError() {
    if (!validationAlert) return;
    validationAlert.textContent = "";
    validationAlert.classList.add("d-none");
}

// ── Helper: Update Connection Badge ──────────────────────────────────────────
function setConnectionStatus(status, bgClass, textClass = "text-white") {
    if (!connectionBadge) return;
    connectionBadge.textContent = status;
    connectionBadge.className = `badge ${bgClass} ${textClass}`;
}

// ── 2. SignalR Hub Event Listeners ───────────────────────────────────────────

// Listener: Receive standard broadcast message
connection.on("ReceiveMessage", function (user, message, timestamp) {
    appendChatMessage(user, message, timestamp);
});

// Listener: Notification when a user joins the chat
connection.on("UserJoined", function (user, timestamp) {
    appendSystemNotification(`👋 ${user} joined the chat.`, timestamp, "text-success");
});

// Listener: Notification when a user leaves/disconnects
connection.on("UserLeft", function (user, timestamp) {
    appendSystemNotification(`🚪 ${user} left the chat.`, timestamp, "text-danger");
});

// Listener: Active connected user count
connection.on("UpdateUserCount", function (count) {
    if (userCountBadge) {
        userCountBadge.textContent = `Users online: ${count}`;
    }
});

// Listener: Server-side validation errors
connection.on("ValidationError", function (errorMessage) {
    showError(`Server: ${errorMessage}`);
});

// ── 3. DOM Rendering Helpers (Safe textContent - No XSS) ─────────────────────

function appendChatMessage(user, message, timestamp) {
    const isSelf = currentUsername && user.toLowerCase() === currentUsername.toLowerCase();

    // Message wrapper
    const messageItem = document.createElement("div");
    messageItem.className = `p-2 rounded ${isSelf ? "bg-primary text-white ms-auto" : "bg-white border text-dark me-auto"}`;
    messageItem.style.maxWidth = "80%";
    messageItem.style.wordBreak = "break-word";

    // Header: User + Timestamp
    const headerDiv = document.createElement("div");
    headerDiv.className = `d-flex align-items-center gap-2 mb-1 ${isSelf ? "text-white-50" : "text-muted"} small`;

    const nameSpan = document.createElement("strong");
    nameSpan.textContent = isSelf ? `${user} (You)` : user;
    if (isSelf) nameSpan.className = "text-white";

    const timeSpan = document.createElement("span");
    timeSpan.textContent = timestamp;

    headerDiv.appendChild(nameSpan);
    headerDiv.appendChild(timeSpan);

    // Body: Message Content (strictly textContent for safety)
    const bodyDiv = document.createElement("div");
    bodyDiv.textContent = message;

    messageItem.appendChild(headerDiv);
    messageItem.appendChild(bodyDiv);

    messagesList.appendChild(messageItem);
    scrollToBottom();
}

function appendSystemNotification(text, timestamp, colorClass = "text-muted") {
    const notifItem = document.createElement("div");
    notifItem.className = `text-center small py-1 my-1 fst-italic ${colorClass}`;

    const textSpan = document.createElement("span");
    textSpan.textContent = `[${timestamp}] ${text}`;

    notifItem.appendChild(textSpan);
    messagesList.appendChild(notifItem);
    scrollToBottom();
}

function scrollToBottom() {
    if (messagesContainer) {
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }
}

// ── 4. User Actions: Join & Send Message ──────────────────────────────────────

async function handleJoin() {
    const rawUser = userInput.value.trim();

    if (!rawUser) {
        showError("Please enter a display name first.");
        userInput.focus();
        return;
    }

    if (rawUser.length > 50) {
        showError("Display name cannot exceed 50 characters.");
        return;
    }

    clearError();
    currentUsername = rawUser;

    try {
        await connection.invoke("JoinChat", currentUsername);
        hasJoined = true;

        // Update UI state
        userInput.readOnly = true;
        joinButton.textContent = "Joined ✓";
        joinButton.classList.remove("btn-outline-primary");
        joinButton.classList.add("btn-success");
        joinButton.disabled = true;

        messageInput.disabled = false;
        sendButton.disabled = false;
        messageInput.focus();
    } catch (err) {
        console.error("Error joining chat:", err);
        showError(`Failed to join: ${err.message || err.toString()}`);
    }
}

async function handleSendMessage() {
    const rawUser = userInput.value.trim();
    const rawMessage = messageInput.value.trim();

    // Validation: Display name
    if (!rawUser) {
        showError("Please enter a display name before sending messages.");
        userInput.focus();
        return;
    }

    // Validation: Empty message or whitespace only
    if (!rawMessage) {
        showError("Message cannot be empty.");
        messageInput.focus();
        return;
    }

    // Validation: Maximum 500 characters
    if (rawMessage.length > 500) {
        showError("Message cannot exceed 500 characters.");
        return;
    }

    clearError();

    // Auto-join if user hasn't explicitly clicked the Join button
    if (!hasJoined) {
        currentUsername = rawUser;
        try {
            await connection.invoke("JoinChat", currentUsername);
            hasJoined = true;
            userInput.readOnly = true;
            joinButton.textContent = "Joined ✓";
            joinButton.classList.remove("btn-outline-primary");
            joinButton.classList.add("btn-success");
            joinButton.disabled = true;
        } catch (err) {
            console.error("Error auto-joining chat:", err);
        }
    }

    try {
        // Send message to Hub
        await connection.invoke("SendMessage", currentUsername, rawMessage);

        // Clear input and reset character counter
        messageInput.value = "";
        if (charCount) charCount.textContent = "0 / 500 characters";
        messageInput.focus();
    } catch (err) {
        console.error("Error sending message:", err);
        showError(`Failed to send message: ${err.message || err.toString()}`);
    }
}

// ── 5. Event Listeners (Keyboard & Clicks) ────────────────────────────────────

// Join button click
joinButton.addEventListener("click", handleJoin);

// Enter key on Display Name triggers Join
userInput.addEventListener("keydown", function (e) {
    if (e.key === "Enter") {
        e.preventDefault();
        handleJoin();
    }
});

// Send button click
sendButton.addEventListener("click", handleSendMessage);

// Enter key on Message Input triggers Send
messageInput.addEventListener("keydown", function (e) {
    if (e.key === "Enter") {
        e.preventDefault();
        handleSendMessage();
    }
});

// Character counter for message input
messageInput.addEventListener("input", function () {
    const len = messageInput.value.length;
    if (charCount) {
        charCount.textContent = `${len} / 500 characters`;
        if (len >= 500) {
            charCount.className = "text-danger fw-bold";
        } else {
            charCount.className = "text-muted";
        }
    }
});

// Clear local chat history button
if (clearChatButton) {
    clearChatButton.addEventListener("click", function () {
        messagesList.innerHTML = "";
        appendSystemNotification("Chat display cleared locally.", new Date().toLocaleTimeString());
    });
}

// ── 6. Reconnection and Lifecycle Handlers ────────────────────────────────────

connection.onreconnecting(function (error) {
    setConnectionStatus("Reconnecting...", "bg-warning", "text-dark");
    sendButton.disabled = true;
    showError("Connection lost. Reconnecting to chat server...");
});

connection.onreconnected(function (connectionId) {
    setConnectionStatus("Connected", "bg-success");
    if (hasJoined) {
        sendButton.disabled = false;
        // Re-register display name after reconnect
        connection.invoke("JoinChat", currentUsername).catch(console.error);
    }
    clearError();
});

connection.onclose(function (error) {
    setConnectionStatus("Disconnected", "bg-danger");
    sendButton.disabled = true;
    showError("Disconnected from chat server. Please refresh the page to reconnect.");
});

// ── 7. Start SignalR Connection ──────────────────────────────────────────────
setConnectionStatus("Connecting...", "bg-warning", "text-dark");

connection.start()
    .then(function () {
        setConnectionStatus("Connected", "bg-success");
        clearError();
        // Enable inputs for user
        messageInput.disabled = false;
        sendButton.disabled = false;
        userInput.focus();
        console.log("SignalR connected successfully.");
    })
    .catch(function (err) {
        console.error("SignalR connection error:", err);
        setConnectionStatus("Connection Failed", "bg-danger");
        showError(`Cannot connect to chat hub: ${err.message || err.toString()}`);
    });
