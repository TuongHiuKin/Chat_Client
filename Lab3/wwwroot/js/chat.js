"use strict";

// ── 1. DOM Element References ────────────────────────────────────────────────
const chatAppContainer = document.getElementById("chatAppContainer");
const currentLoggedUserEl = document.getElementById("currentLoggedUser");
const messageInput = document.getElementById("messageInput");
const sendButton = document.getElementById("sendButton");
const messagesContainer = document.getElementById("messagesContainer");
const messagesList = document.getElementById("messagesList");
const connectionBadge = document.getElementById("connectionBadge");
const userCountBadge = document.getElementById("userCountBadge");
const validationAlert = document.getElementById("validationAlert");
const charCount = document.getElementById("charCount");
const clearChatButton = document.getElementById("clearChatButton");
const onlineUsersList = document.getElementById("onlineUsersList");
const onlineCounterBadge = document.getElementById("onlineCounterBadge");

// Toolbar & Upload Elements
const emojiToggleBtn = document.getElementById("emojiToggleBtn");
const emojiPicker = document.getElementById("emojiPicker");
const emojiGrid = document.getElementById("emojiGrid");
const emojiTabs = document.getElementById("emojiTabs");
const closeEmojiPickerBtn = document.getElementById("closeEmojiPickerBtn");

const pickImageBtn = document.getElementById("pickImageBtn");
const pickFileBtn = document.getElementById("pickFileBtn");
const imageFileInput = document.getElementById("imageFileInput");
const generalFileInput = document.getElementById("generalFileInput");

// Upload Progress Elements
const uploadProgressWrapper = document.getElementById("uploadProgressWrapper");
const uploadFileName = document.getElementById("uploadFileName");
const uploadProgressBar = document.getElementById("uploadProgressBar");
const uploadStatusText = document.getElementById("uploadStatusText");
const cancelUploadBtn = document.getElementById("cancelUploadBtn");

// Preview & Lightbox Modals
const imagePreviewModalEl = document.getElementById("imagePreviewModal");
const previewModalImage = document.getElementById("previewModalImage");
const previewModalFileName = document.getElementById("previewModalFileName");
const previewModalFileSize = document.getElementById("previewModalFileSize");
const previewModalFileType = document.getElementById("previewModalFileType");
const previewModalCaption = document.getElementById("previewModalCaption");
const confirmSendImageBtn = document.getElementById("confirmSendImageBtn");

const lightboxModalEl = document.getElementById("lightboxModal");
const lightboxImage = document.getElementById("lightboxImage");
const lightboxTitle = document.getElementById("lightboxTitle");
const lightboxDownloadBtn = document.getElementById("lightboxDownloadBtn");

let previewModalInstance = null;
let lightboxModalInstance = null;
if (typeof bootstrap !== "undefined") {
    if (imagePreviewModalEl) previewModalInstance = new bootstrap.Modal(imagePreviewModalEl);
    if (lightboxModalEl) lightboxModalInstance = new bootstrap.Modal(lightboxModalEl);
}

// ── 2. State & Variables ─────────────────────────────────────────────────────
// Tên người dùng được truyền từ tài khoản đã đăng nhập
let currentUsername = (chatAppContainer && chatAppContainer.dataset.currentUser) 
    ? chatAppContainer.dataset.currentUser 
    : (currentLoggedUserEl ? currentLoggedUserEl.textContent.trim() : "User");

let hasJoined = true;
let currentPendingImageFile = null;
let currentUploadXhr = null;

// Danh mục Emoji y hệt Lab 2
const emojiCategories = {
    smileys: [
        "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "🙃",
        "😉", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😋",
        "😛", "😜", "🤪", "😝", "🤑", "🤗", "🤭", "🤫", "🤔", "🤐",
        "🤨", "😐", "😑", "😶", "😏", "😒", "🙄", "😬", "😮‍💨", "🤥",
        "😌", "😔", "😪", "🤤", "😴", "😷", "🤒", "🤕", "🤢", "🤮",
        "🤧", "🥵", "🥶", "🥴", "😵", "🤯", "🤠", "🥳", "🥸", "😎",
        "🤓", "🧐", "😕", "😟", "🙁", "😮", "😯", "😲", "😳", "🥺",
        "😦", "😧", "😨", "😰", "😥", "😢", "😭", "😱", "😖", "😣",
        "😞", "😓", "😩", "😫", "🥱", "😤", "😡", "😠", "🤬", "😈",
        "👿", "💀", "☠", "💩", "🤡", "👹", "👺", "👻", "👽", "🤖"
    ],
    hearts: [
        "💖", "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎",
        "💔", "❣️", "💕", "💞", "💓", "💗", "💘", "💝", "💟", "💌",
        "💋", "🫂", "😻", "😽", "💑", "👩‍❤️‍👨", "💏", "💐", "🌹", "🌸",
        "🌺", "🌷", "🌻", "🥀", "🌼", "🌿", "🍀", "✨", "🌟", "💫"
    ],
    gestures: [
        "👍", "👎", "👊", "✊", "🤛", "🤜", "👏", "🙌", "👐", "🤲",
        "🤝", "🙏", "✍", "💅", "🤳", "💪", "🦾", "🦵", "🦶", "👂",
        "👃", "👁", "👀", "👅", "👄", "👈", "👉", "👆", "👇", "☝",
        "🖐", "✋", "🤚", "🖖", "👋", "🤙", "🤏", "✌", "🤞", "🤟"
    ],
    food: [
        "🍔", "🍟", "🍕", "🌭", "🥪", "🌮", "🌯", "🥗", "🥘", "🍝",
        "🍜", "🍲", "🍛", "🍣", "🍱", "🥟", "🍤", "🍙", "🍚", "🍘",
        "🍦", "🍧", "🍨", "🍩", "🍪", "🎂", "🍰", "🧁", "🥧", "🍫",
        "🍬", "🍭", "🍮", "🍯", "☕", "🍵", "🧃", "🥤", "🧋", "🍺",
        "🍻", "🥂", "🍷", "🥃", "🍸", "🍹", "🍾", "🍿", "🥑", "🍓"
    ],
    fun: [
        "🎉", "🎊", "🎈", "🎁", "🎀", "🎆", "🎇", "🧨", "✨", "🌟",
        "🎵", "🎶", "🎸", "🎹", "🥁", "🎺", "🎻", "🎲", "🎯", "🎳",
        "🎮", "🕹", "🎰", "🏆", "🥇", "🥈", "🥉", "🏅", "⚽", "🏀",
        "🏈", "⚾", "🎾", "🏐", "🎱", "🏓", "🏸", "🥊", "🥋", "🛹"
    ],
    objects: [
        "🚀", "🛸", "🚁", "✈", "🚗", "🏎", "🏍", "🚲", "🚂", "🚢",
        "🏝", "🏖", "🌋", "🏰", "💻", "🖥", "📱", "⌚", "💡", "🔦",
        "🕯", "💰", "💵", "💎", "🔑", "🔒", "🔮", "🪄", "🧪", "🧬",
        "🔭", "🌈", "⚡", "🔥", "❄", "⭐", "☀️", "🌙", "☁️", "🪐"
    ]
};

// 192 mã hex emoji có tài nguyên ảnh PNG (giống Lab 2)
const pngEmojiCodes = new Set([
    "1f308","1f319","1f31f","1f32d","1f32e","1f338","1f339","1f354","1f355","1f35c",
    "1f35d","1f35f","1f363","1f366","1f369","1f36a","1f36b","1f36c","1f36d","1f370",
    "1f371","1f375","1f377","1f37a","1f37b","1f380","1f381","1f382","1f386","1f387",
    "1f388","1f389","1f38a","1f3a4","1f3ae","1f3af","1f3b1","1f3b2","1f3b5","1f3b6",
    "1f3b8","1f3b9","1f3be","1f3c0","1f3c6","1f3c8","1f3cd","1f3ce","1f446","1f447",
    "1f448","1f449","1f44b","1f44c","1f44d","1f44e","1f44f","1f450","1f451","1f47b",
    "1f47d","1f480","1f48b","1f48c","1f48e","1f490","1f493","1f494","1f495","1f496",
    "1f497","1f498","1f499","1f49a","1f49b","1f49c","1f49d","1f49e","1f4a1","1f4a9",
    "1f4aa","1f4af","1f4b0","1f4b5","1f4bb","1f4f1","1f511","1f512","1f514","1f525",
    "1f52e","1f579","1f590","1f596","1f5a4","1f600","1f601","1f602","1f603","1f604",
    "1f605","1f606","1f607","1f608","1f609","1f60a","1f60b","1f60c","1f60d","1f60e",
    "1f610","1f618","1f61b","1f61c","1f61d","1f621","1f622","1f624","1f62c","1f62d",
    "1f631","1f634","1f635","1f637","1f642","1f644","1f64c","1f64f","1f680","1f697",
    "1f6b2","1f90d","1f90e","1f90f","1f910","1f911","1f912","1f913","1f914","1f915",
    "1f916","1f917","1f918","1f919","1f91d","1f91e","1f91f","1f920","1f921","1f922",
    "1f923","1f925","1f927","1f928","1f929","1f92a","1f92b","1f92c","1f92d","1f92e",
    "1f92f","1f933","1f942","1f947","1f95f","1f964","1f96a","1f970","1f973","1f974",
    "1f975","1f976","1f97a","1f9cb","1f9d0","1f9e1","1f9e8","1fac2","231a","2600",
    "2615","261d","26a1","26bd","26be","2708","270b","270c","270d","2728","2764","2b50"
]);

// ── 3. Establish SignalR Connection ──────────────────────────────────────────
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

// ── 4. SignalR Hub Event Listeners ───────────────────────────────────────────
connection.on("ReceiveMessage", function (user, message, timestamp) {
    appendChatMessage(user, message, timestamp);
});

connection.on("ReceiveAttachment", function (user, attachment, timestamp) {
    appendAttachmentMessage(user, attachment, timestamp);
});

connection.on("UserJoined", function (user, timestamp) {
    appendSystemNotification(`👋 ${user} đã tham gia phòng chat.`, timestamp, "text-success");
});

connection.on("UserLeft", function (user, timestamp) {
    appendSystemNotification(`🚪 ${user} đã rời phòng chat.`, timestamp, "text-danger");
});

connection.on("UpdateUserCount", function (count) {
    if (userCountBadge) {
        userCountBadge.textContent = `Online: ${count}`;
    }
    if (onlineCounterBadge) {
        onlineCounterBadge.textContent = count;
    }
});

connection.on("UpdateOnlineUsers", function (users) {
    renderOnlineUsersList(users);
});

connection.on("ValidationError", function (errorMessage) {
    showError(`Lỗi máy chủ: ${errorMessage}`);
});

// ── 5. Helper Functions ──────────────────────────────────────────────────────
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

function setConnectionStatus(status, bgClass, textClass = "text-white") {
    if (!connectionBadge) return;
    connectionBadge.textContent = status;
    connectionBadge.className = `badge ${bgClass} ${textClass} me-2`;
}

function formatBytes(bytes) {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
}

function getFileIcon(fileName) {
    const ext = fileName.split(".").pop().toLowerCase();
    switch (ext) {
        case "pdf": return "📕";
        case "doc":
        case "docx": return "📘";
        case "xls":
        case "xlsx": return "📗";
        case "ppt":
        case "pptx": return "📙";
        case "zip":
        case "rar":
        case "7z":
        case "tar": return "📦";
        case "mp3":
        case "wav":
        case "ogg": return "🎵";
        case "mp4":
        case "mkv":
        case "avi": return "🎬";
        case "cs":
        case "js":
        case "ts":
        case "html":
        case "css":
        case "json": return "💻";
        case "txt": return "📝";
        default: return "📄";
    }
}

// Chuyển ký tự Emoji sang hex codepoint (bỏ variant 0xfe0f)
function getEmojiCodePoint(emojiStr) {
    const codePoints = [];
    for (const char of emojiStr) {
        const cp = char.codePointAt(0);
        if (cp && cp !== 0xfe0f) {
            codePoints.push(cp.toString(16).toLowerCase());
        }
    }
    return codePoints.join("-");
}

// Render nội dung text có emoji màu (nếu có PNG thì dùng ảnh, không thì dùng Unicode)
function createEmojiRenderedContent(text) {
    const span = document.createElement("span");
    const segments = Array.from(text);
    let buffer = "";

    function flushBuffer() {
        if (buffer) {
            span.appendChild(document.createTextNode(buffer));
            buffer = "";
        }
    }

    for (const char of segments) {
        const code = getEmojiCodePoint(char);
        if (pngEmojiCodes.has(code)) {
            flushBuffer();
            const img = document.createElement("img");
            img.src = `/emojis/${code}.png`;
            img.className = "emoji-img";
            img.alt = char;
            img.title = char;
            img.loading = "lazy";
            span.appendChild(img);
        } else {
            buffer += char;
        }
    }
    flushBuffer();
    return span;
}

// ── 6. UI Append Methods ─────────────────────────────────────────────────────

function renderOnlineUsersList(users) {
    if (!onlineUsersList) return;
    onlineUsersList.innerHTML = "";
    const list = users || [];

    if (onlineCounterBadge) {
        onlineCounterBadge.textContent = list.length;
    }
    if (userCountBadge) {
        userCountBadge.textContent = `Online: ${list.length}`;
    }

    if (list.length === 0) {
        onlineUsersList.innerHTML = '<div class="text-muted small text-center my-3 fst-italic">Không có ai trực tuyến</div>';
        return;
    }

    list.forEach(user => {
        const isSelf = currentUsername && user.toLowerCase() === currentUsername.toLowerCase();
        const item = document.createElement("div");
        item.className = `d-flex align-items-center justify-content-between p-2 rounded mb-1 ${isSelf ? "bg-white border border-primary shadow-sm" : "bg-white border"}`;

        const left = document.createElement("div");
        left.className = "d-flex align-items-center gap-2 text-truncate";

        const dot = document.createElement("span");
        dot.className = "online-dot";
        dot.title = "Đang trực tuyến";

        const nameSpan = document.createElement("span");
        nameSpan.className = "fw-semibold text-truncate small";
        nameSpan.textContent = isSelf ? `${user} (Bạn)` : user;
        if (isSelf) nameSpan.classList.add("text-primary");

        left.appendChild(dot);
        left.appendChild(nameSpan);

        const badge = document.createElement("span");
        badge.className = "badge bg-success-subtle text-success border border-success-subtle small py-1 px-2";
        badge.textContent = "Online";
        badge.style.fontSize = "0.7rem";

        item.appendChild(left);
        item.appendChild(badge);
        onlineUsersList.appendChild(item);
    });
}

function appendSystemNotification(message, timestamp, textClass) {
    if (!messagesList) return;
    const notificationDiv = document.createElement("div");
    notificationDiv.className = `text-center small ${textClass} my-1 py-1 px-3 bg-light rounded-pill align-self-center`;
    notificationDiv.innerHTML = `<span class="text-muted me-1">[${timestamp}]</span> <strong>${message}</strong>`;
    messagesList.appendChild(notificationDiv);
    scrollToBottom();
}

function appendChatMessage(user, message, timestamp) {
    if (!messagesList) return;
    const isSelf = currentUsername && user.toLowerCase() === currentUsername.toLowerCase();

    const bubble = document.createElement("div");
    bubble.className = `chat-bubble p-2 ${isSelf ? "chat-bubble-self ms-auto" : "chat-bubble-other me-auto"}`;
    bubble.style.maxWidth = "75%";
    bubble.style.wordBreak = "break-word";

    // Header: Tên & giờ
    const header = document.createElement("div");
    header.className = `d-flex align-items-center gap-2 mb-1 ${isSelf ? "text-white-50" : "text-muted"} small`;

    const nameSpan = document.createElement("strong");
    nameSpan.textContent = isSelf ? `${user} (Bạn)` : user;
    if (isSelf) nameSpan.className = "text-white";

    const timeSpan = document.createElement("span");
    timeSpan.textContent = timestamp;

    header.appendChild(nameSpan);
    header.appendChild(timeSpan);
    bubble.appendChild(header);

    // Body: Nội dung emoji
    const bodyContent = createEmojiRenderedContent(message);
    bubble.appendChild(bodyContent);

    messagesList.appendChild(bubble);
    scrollToBottom();
}

function appendAttachmentMessage(user, attachment, timestamp) {
    if (!messagesList) return;
    const isSelf = currentUsername && user.toLowerCase() === currentUsername.toLowerCase();

    const bubble = document.createElement("div");
    bubble.className = `chat-bubble p-2 ${isSelf ? "chat-bubble-self ms-auto" : "chat-bubble-other me-auto"}`;
    bubble.style.maxWidth = "80%";
    bubble.style.wordBreak = "break-word";

    // Header
    const header = document.createElement("div");
    header.className = `d-flex align-items-center gap-2 mb-1 ${isSelf ? "text-white-50" : "text-muted"} small`;

    const nameSpan = document.createElement("strong");
    nameSpan.textContent = isSelf ? `${user} (Bạn)` : user;
    if (isSelf) nameSpan.className = "text-white";

    const timeSpan = document.createElement("span");
    timeSpan.textContent = timestamp;

    header.appendChild(nameSpan);
    header.appendChild(timeSpan);
    bubble.appendChild(header);

    // Content: Hình ảnh hoặc Tệp tin
    if (attachment.isImage) {
        // Khung ảnh
        const imgWrapper = document.createElement("div");
        imgWrapper.className = "chat-image-wrapper mt-1 mb-1 text-center";

        const img = document.createElement("img");
        img.src = attachment.fileUrl;
        img.className = "chat-thumbnail";
        img.alt = attachment.fileName;
        img.title = "Click để phóng to ảnh";
        img.addEventListener("click", () => openLightbox(attachment.fileUrl, attachment.fileName));

        imgWrapper.appendChild(img);
        bubble.appendChild(imgWrapper);

        // Caption (nếu có)
        if (attachment.caption) {
            const captionDiv = document.createElement("div");
            captionDiv.className = "small mt-1 px-1";
            captionDiv.appendChild(createEmojiRenderedContent(attachment.caption));
            bubble.appendChild(captionDiv);
        }

        // Tải ảnh gốc
        const dlWrapper = document.createElement("div");
        dlWrapper.className = "d-flex justify-content-between align-items-center mt-1 pt-1 border-top border-opacity-25 small";
        dlWrapper.innerHTML = `
            <span class="${isSelf ? "text-white-50" : "text-muted"}">${formatBytes(attachment.fileSize)}</span>
            <a href="${attachment.fileUrl}" download="${attachment.fileName}" class="text-decoration-none ${isSelf ? "text-white fw-bold" : "text-primary fw-bold"}" target="_blank">
                ⬇️ Tải ảnh gốc
            </a>
        `;
        bubble.appendChild(dlWrapper);
    } else {
        // Thẻ tệp tin File Card
        const fileCard = document.createElement("a");
        fileCard.href = attachment.fileUrl;
        fileCard.download = attachment.fileName;
        fileCard.className = "chat-file-card mt-1";
        fileCard.target = "_blank";

        const icon = document.createElement("span");
        icon.className = "chat-file-icon";
        icon.textContent = getFileIcon(attachment.fileName);

        const infoDiv = document.createElement("div");
        infoDiv.className = "text-truncate";

        const fileNameDiv = document.createElement("div");
        fileNameDiv.className = "fw-bold text-truncate";
        fileNameDiv.textContent = attachment.fileName;

        const sizeDiv = document.createElement("div");
        sizeDiv.className = "small opacity-75";
        sizeDiv.textContent = `${formatBytes(attachment.fileSize)} • Nhấp để tải về`;

        infoDiv.appendChild(fileNameDiv);
        infoDiv.appendChild(sizeDiv);

        fileCard.appendChild(icon);
        fileCard.appendChild(infoDiv);
        bubble.appendChild(fileCard);
    }

    messagesList.appendChild(bubble);
    scrollToBottom();
}

function scrollToBottom() {
    if (messagesContainer) {
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }
}

function openLightbox(url, title) {
    if (lightboxImage) lightboxImage.src = url;
    if (lightboxTitle) lightboxTitle.textContent = title || "Xem ảnh phóng to";
    if (lightboxDownloadBtn) {
        lightboxDownloadBtn.href = url;
        lightboxDownloadBtn.download = title || "image";
    }
    if (lightboxModalInstance) lightboxModalInstance.show();
}

// ── 7. Emoji Picker Interaction ──────────────────────────────────────────────
function renderEmojiCategory(categoryKey) {
    if (!emojiGrid) return;
    emojiGrid.innerHTML = "";
    const list = emojiCategories[categoryKey] || emojiCategories.smileys;

    list.forEach(emoji => {
        const item = document.createElement("div");
        item.className = "emoji-item";
        item.title = emoji;

        const code = getEmojiCodePoint(emoji);
        if (pngEmojiCodes.has(code)) {
            const img = document.createElement("img");
            img.src = `/emojis/${code}.png`;
            img.className = "emoji-img-large";
            img.alt = emoji;
            item.appendChild(img);
        } else {
            item.textContent = emoji;
        }

        item.addEventListener("click", () => {
            insertEmojiToInput(emoji);
        });

        emojiGrid.appendChild(item);
    });
}

function insertEmojiToInput(emoji) {
    if (!messageInput) return;
    const start = messageInput.selectionStart ?? messageInput.value.length;
    const end = messageInput.selectionEnd ?? messageInput.value.length;
    const text = messageInput.value;
    messageInput.value = text.substring(0, start) + emoji + text.substring(end);
    messageInput.focus();
    const newPos = start + emoji.length;
    messageInput.setSelectionRange(newPos, newPos);
    updateCharCounter();
}

if (emojiToggleBtn) {
    emojiToggleBtn.addEventListener("click", (e) => {
        e.stopPropagation();
        if (emojiPicker.classList.contains("d-none")) {
            renderEmojiCategory("smileys");
            emojiPicker.classList.remove("d-none");
        } else {
            emojiPicker.classList.add("d-none");
        }
    });
}

if (closeEmojiPickerBtn) {
    closeEmojiPickerBtn.addEventListener("click", () => {
        emojiPicker.classList.add("d-none");
    });
}

if (emojiTabs) {
    emojiTabs.addEventListener("click", (e) => {
        const btn = e.target.closest(".emoji-tab-btn");
        if (!btn) return;
        emojiTabs.querySelectorAll(".emoji-tab-btn").forEach(b => b.classList.remove("active"));
        btn.classList.add("active");
        renderEmojiCategory(btn.dataset.category);
    });
}

// Click ngoài thì đóng Emoji Picker
document.addEventListener("click", (e) => {
    if (emojiPicker && !emojiPicker.classList.contains("d-none")) {
        if (!emojiPicker.contains(e.target) && e.target !== emojiToggleBtn) {
            emojiPicker.classList.add("d-none");
        }
    }
});

// ── 8. Upload File & Image Handlers ──────────────────────────────────────────

// Chọn ảnh -> Mở Preview Modal
if (pickImageBtn) {
    pickImageBtn.addEventListener("click", () => {
        if (imageFileInput) imageFileInput.click();
    });
}

if (imageFileInput) {
    imageFileInput.addEventListener("change", function () {
        if (!this.files || this.files.length === 0) return;
        const file = this.files[0];

        // Kiểm tra dung lượng tối đa 500 MB
        if (file.size > 524288000) {
            showError("Ảnh vượt quá kích thước cho phép (tối đa 500 MB).");
            this.value = "";
            return;
        }

        currentPendingImageFile = file;
        if (previewModalFileName) previewModalFileName.textContent = file.name;
        if (previewModalFileSize) previewModalFileSize.textContent = `Kích thước: ${formatBytes(file.size)}`;
        if (previewModalFileType) previewModalFileType.textContent = file.type || "Hình ảnh";
        if (previewModalCaption) previewModalCaption.value = "";

        const objectUrl = URL.createObjectURL(file);
        if (previewModalImage) previewModalImage.src = objectUrl;

        if (previewModalInstance) previewModalInstance.show();
        this.value = "";
    });
}

// Xác nhận gửi ảnh từ Preview Modal
if (confirmSendImageBtn) {
    confirmSendImageBtn.addEventListener("click", async () => {
        if (!currentPendingImageFile) return;
        const file = currentPendingImageFile;
        const caption = previewModalCaption ? previewModalCaption.value.trim() : "";
        if (previewModalInstance) previewModalInstance.hide();

        await uploadAndSendAttachment(file, true, caption);
        currentPendingImageFile = null;
    });
}

// Chọn tệp tin -> Gửi trực tiếp có progress
if (pickFileBtn) {
    pickFileBtn.addEventListener("click", () => {
        if (generalFileInput) generalFileInput.click();
    });
}

if (generalFileInput) {
    generalFileInput.addEventListener("change", async function () {
        if (!this.files || this.files.length === 0) return;
        const file = this.files[0];

        if (file.size > 524288000) {
            showError("Tập tin vượt quá kích thước cho phép (tối đa 500 MB).");
            this.value = "";
            return;
        }

        await uploadAndSendAttachment(file, false, "");
        this.value = "";
    });
}

// Hàm upload tập tin/hình ảnh với thanh tiến độ Ajax
function uploadAndSendAttachment(file, isImage, caption) {
    return new Promise((resolve, reject) => {
        clearError();

        // Hiển thị thanh tiến độ
        if (uploadProgressWrapper) {
            uploadProgressWrapper.classList.remove("d-none");
            uploadFileName.textContent = `Đang gửi: ${file.name} (${formatBytes(file.size)})`;
            uploadProgressBar.style.width = "0%";
            uploadStatusText.textContent = "0%";
        }

        const formData = new FormData();
        formData.append("file", file);

        const xhr = new XMLHttpRequest();
        currentUploadXhr = xhr;

        xhr.upload.addEventListener("progress", (e) => {
            if (e.lengthComputable) {
                const percent = Math.round((e.loaded / e.total) * 100);
                if (uploadProgressBar) uploadProgressBar.style.width = `${percent}%`;
                if (uploadStatusText) uploadStatusText.textContent = `${percent}% (${formatBytes(e.loaded)} / ${formatBytes(e.total)})`;
            }
        });

        xhr.addEventListener("load", async () => {
            currentUploadXhr = null;
            if (uploadProgressWrapper) uploadProgressWrapper.classList.add("d-none");

            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const response = JSON.parse(xhr.responseText);
                    if (response.success) {
                        // Gửi thông điệp Attachment qua SignalR
                        await connection.invoke("SendAttachment", currentUsername, response.fileName, response.fileSize, response.fileUrl, response.isImage, caption);
                        resolve();
                    } else {
                        showError(response.message || "Tải lên thất bại.");
                        reject(new Error(response.message));
                    }
                } catch (err) {
                    showError("Lỗi đọc phản hồi từ máy chủ.");
                    reject(err);
                }
            } else {
                showError(`Lỗi máy chủ (${xhr.status}): Không thể tải lên tệp tin.`);
                reject(new Error(xhr.statusText));
            }
        });

        xhr.addEventListener("error", () => {
            currentUploadXhr = null;
            if (uploadProgressWrapper) uploadProgressWrapper.classList.add("d-none");
            showError("Lỗi kết nối khi truyền dữ liệu.");
            reject(new Error("Upload network error"));
        });

        xhr.addEventListener("abort", () => {
            currentUploadXhr = null;
            if (uploadProgressWrapper) uploadProgressWrapper.classList.add("d-none");
            showError("Đã hủy quá trình truyền tệp tin.");
            resolve();
        });

        xhr.open("POST", "/api/chat/upload", true);
        xhr.send(formData);
    });
}

// Nút Hủy truyền file
if (cancelUploadBtn) {
    cancelUploadBtn.addEventListener("click", () => {
        if (currentUploadXhr) {
            currentUploadXhr.abort();
            currentUploadXhr = null;
        }
    });
}

// ── 9. Chat Send Handlers ───────────────────────────────────────────────────

function sendTextMessage() {
    clearError();
    const rawMessage = messageInput ? messageInput.value.trim() : "";
    if (!rawMessage) return;

    if (rawMessage.length > 500) {
        showError("Tin nhắn không được vượt quá 500 ký tự.");
        return;
    }

    if (sendButton) sendButton.disabled = true;

    connection.invoke("SendMessage", currentUsername, rawMessage)
        .then(() => {
            if (messageInput) {
                messageInput.value = "";
                messageInput.focus();
            }
            updateCharCounter();
        })
        .catch(err => {
            showError(`Gửi tin nhắn thất bại: ${err.toString()}`);
        })
        .finally(() => {
            if (sendButton) sendButton.disabled = false;
        });
}

function updateCharCounter() {
    if (!charCount || !messageInput) return;
    const len = messageInput.value.length;
    charCount.textContent = `${len} / 500 ký tự`;
    charCount.className = len > 450 ? "text-danger fw-bold" : "text-muted";
}

// ── 10. Event Bindings ───────────────────────────────────────────────────────

if (sendButton) {
    sendButton.addEventListener("click", sendTextMessage);
}

if (messageInput) {
    messageInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendTextMessage();
        }
    });
    messageInput.addEventListener("input", updateCharCounter);
}

if (clearChatButton) {
    clearChatButton.addEventListener("click", () => {
        if (messagesList) messagesList.innerHTML = "";
    });
}

// ── 11. Lifecycle & Auto-Reconnect ──────────────────────────────────────────

connection.onreconnecting(error => {
    setConnectionStatus("Đang kết nối lại...", "bg-warning", "text-dark");
    showError("Đang thử kết nối lại đến máy chủ...");
});

connection.onreconnected(connectionId => {
    setConnectionStatus("Đã kết nối", "bg-success");
    clearError();
    if (currentUsername) {
        connection.invoke("JoinChat", currentUsername).catch(console.error);
    }
});

connection.onclose(error => {
    setConnectionStatus("Mất kết nối", "bg-danger");
    showError("Mất kết nối tới máy chủ. Vui lòng làm mới trang.");
    if (messageInput) messageInput.disabled = true;
    if (sendButton) sendButton.disabled = true;
});

// Bắt đầu kết nối và tự động JoinChat bằng tài khoản đã đăng nhập
setConnectionStatus("Đang kết nối...", "bg-warning", "text-dark");
connection.start()
    .then(() => {
        setConnectionStatus("Đã kết nối", "bg-success");
        // Tự động gia nhập chat với tên người dùng đã đăng nhập
        if (currentUsername) {
            connection.invoke("JoinChat", currentUsername).catch(err => {
                console.error("Auto JoinChat error:", err);
            });
        }
        if (messageInput) messageInput.focus();
    })
    .catch(err => {
        setConnectionStatus("Lỗi kết nối", "bg-danger");
        showError(`Không thể khởi tạo kết nối SignalR: ${err.toString()}`);
    });
