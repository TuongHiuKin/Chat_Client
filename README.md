# 💬 ChatSystem - Hệ Thống Chat Real-Time (.NET 10 & TCP Socket)

> **Đồ án môn học PRN222** - Ứng dụng chat đa người dùng kiến trúc Client - Server sử dụng Raw TCP Socket, định dạng gói tin JSON tùy biến, Entity Framework Core và giao diện WPF hiện đại.

---

## ⚡ Tải Nhanh & Trải Nghiệm Ngay (Quick Start)

Dành cho người muốn chạy và trải nghiệm ngay lập tức **không cần cài đặt .NET SDK, không cần build mã nguồn, không cần cài đặt SQL Server**:

| Thành phần | Đường dẫn tải / Lệnh chạy | Ghi chú |
| :--- | :--- | :--- |
| 💻 **ChatClient (Giao diện người dùng)** | [👉 **Bấm vào đây để tải `ChatClient.exe` (GitHub Releases)**](https://github.com/TuongHiuKin/Chat_Client/releases/latest/download/ChatClient.exe)<br>*(Hoặc xem danh sách phiên bản tại [Releases Page](https://github.com/TuongHiuKin/Chat_Client/releases))* | Ứng dụng độc lập (Self-contained), tải về nhấp đúp là chạy ngay trên Windows |
| 🐳 **ChatServer (Docker Hub Image)** | [👉 **Xem Image trên Docker Hub: `tuongkien/chat-server`**](https://hub.docker.com/r/tuongkien/chat-server) | Lưu trữ chính thức trên Docker Hub |
| 🚀 **Lệnh kéo Docker Image về máy** | `docker pull tuongkien/chat-server:latest` | Kéo image Server đã build sẵn từ Docker Hub |

### 🏃 Chạy toàn bộ hệ thống (Server + CSDL) chỉ với 1 câu lệnh:
Chỉ cần tải file [**`docker-compose.yml`**](https://raw.githubusercontent.com/TuongHiuKin/Chat_Client/feature/docker/docker-compose.yml) về máy và chạy lệnh sau trong Terminal:
```bash
docker compose up -d
```
> 💡 **Hệ thống sẽ tự động:**
> 1. Kéo image `tuongkien/chat-server:latest` từ Docker Hub.
> 2. Kéo image CSDL Microsoft SQL Server 2022.
> 3. Tự tạo CSDL `ChatDB`, toàn bộ bảng và mở cổng `5000`.
> 4. Bây giờ bạn chỉ cần mở file `ChatClient.exe` lên là chat được ngay!

---

## 📑 Mục lục
- [1. Hướng dẫn sử dụng cho người dùng](#1-hướng-dẫn-sử-dụng-cho-người-dùng)
- [2. Hướng dẫn dành cho Lập trình viên (Build & Push Docker Hub)](#2-hướng-dẫn-dành-cho-lập-trình-viên-build--push-docker-hub)
- [3. Hướng dẫn chạy thủ công (Không dùng Docker)](#3-hướng-dẫn-chạy-thủ-công-không-dùng-docker)
- [4. Kiến trúc hệ thống](#4-kiến-trúc-hệ-thống)
- [5. Cấu trúc thư mục mã nguồn](#5-cấu-trúc-thư-mục-mã-nguồn)
- [6. Các tính năng nổi bật](#6-các-tính-năng-nổi-bật)

---

## 1. Hướng dẫn sử dụng cho người dùng

### Bước 1: Khởi chạy Backend (Server & DB)
Mở Terminal tại thư mục chứa `docker-compose.yml` và chạy:
```bash
docker compose up -d
```
Kiểm tra server đã sẵn sàng chưa:
```bash
docker compose logs -f chatserver
```
*(Khi thấy dòng `╔═ CHAT SERVER STARTED ═╗` và `Waiting for clients...` là đã sẵn sàng)*.

### Bước 2: Mở ứng dụng ChatClient
1. Tải file [**`ChatClient.exe`**](https://github.com/TuongHiuKin/Chat_Client/releases/latest/download/ChatClient.exe) về máy.
2. Nhấp đúp mở **2 cửa sổ `ChatClient.exe`** để thử nghiệm chat giữa 2 tài khoản:
   * **Host:** `127.0.0.1` (hoặc IP mạng LAN của máy chủ nếu chạy khác máy).
   * **Port:** `5000`.
3. Đăng ký tài khoản:
   * Cửa sổ 1: Đăng ký tài khoản `alice` (Mật khẩu: `123456`, Tên hiển thị: `Alice`).
   * Cửa sổ 2: Đăng ký tài khoản `bob` (Mật khẩu: `123456`, Tên hiển thị: `Bob`).
4. Bắt đầu chat:
   * Tại cửa sổ Alice, nhấn nút **+** $\rightarrow$ Chọn **Bob** $\rightarrow$ Nhấn **Tạo hội thoại**.
   * Nhắn tin văn bản, chọn Emoji, gửi ảnh hoặc tài liệu đính kèm theo thời gian thực!

### Bước 3: Tắt hệ thống khi dùng xong
```bash
docker compose down
```
*(Dữ liệu tin nhắn và tài khoản vẫn được bảo toàn trong Docker Volume `mssql_data`).*

---

## 2. Hướng dẫn dành cho Lập trình viên (Build & Push Docker Hub)

Nếu bạn sửa đổi mã nguồn của Server và muốn cập nhật image mới lên Docker Hub:

### 1. Đăng nhập Docker Hub
```bash
docker login
```
*(Nhập Username: `tuongkien` và Password/Personal Access Token)*.

### 2. Build và Push Image lên Docker Hub
Tại thư mục `ChatSystem/`, chạy:
```bash
# Build image với tag tuongkien/chat-server:latest
docker compose build

# Đẩy image lên Docker Hub
docker compose push
```
Hoặc dùng lệnh `docker build / push` truyền thống:
```bash
docker build -t tuongkien/chat-server:latest -f ChatServer/Dockerfile .
docker push tuongkien/chat-server:latest
```

### 3. Đính kèm file `ChatClient.exe` lên GitHub Releases
Vì file `ChatClient.exe` có dung lượng ~139 MB (vượt quá giới hạn 100 MB của Git thông thường), cách chuyên nghiệp nhất là đính kèm vào **GitHub Releases**:
1. Truy cập [GitHub Repository Releases](https://github.com/TuongHiuKin/Chat_Client/releases).
2. Nhấn **Draft a new release** (Tạo release mới).
3. Đặt Tag: `v1.0.0`, Tiêu đề: `ChatClient Release v1.0.0`.
4. Kéo thả file `ChatClient.exe` vào khung đính kèm file (Attach binaries by dropping them here).
5. Nhấn **Publish release** $\rightarrow$ Link tải [ChatClient.exe](https://github.com/TuongHiuKin/Chat_Client/releases/latest/download/ChatClient.exe) ở đầu trang sẽ hoạt động ngay lập tức!

---

## 3. Hướng dẫn chạy thủ công (Không dùng Docker)

Dành cho môi trường phát triển cục bộ bằng Visual Studio hoặc .NET CLI:

1. **Cấu hình SQL Server:**
   * Copy `ChatServer/appsettings.example.json` thành `ChatServer/appsettings.json`.
   * Cập nhật thông tin tài khoản SQL Server của bạn.
2. **Chạy Server:**
   ```bash
   cd ChatServer
   dotnet run
   ```
   *Server sẽ tự động kết nối và khởi tạo database `ChatDB` nếu chưa có.*
3. **Chạy Client:**
   ```bash
   cd ChatClient
   dotnet run
   ```

---

## 4. Kiến trúc hệ thống

```text
+-------------------------------------------------------------+
|                     ChatClient (WPF App)                    |
|             (Chạy trực tiếp trên máy người dùng)            |
+-------------------------------------------------------------+
                              ▲
                              │  TCP Socket / JSON (\n framed)
                              │  Port 5000
                              ▼
+-------------------------------------------------------------+
|               ChatServer (Docker Container)                 |
|             Image: tuongkien/chat-server:latest           |
|           - TcpListener lắng nghe kết nối đa luồng           |
|           - Broadcast tin nhắn theo thời gian thực          |
|           - Tự động đồng bộ schema CSDL với EF Core         |
+-------------------------------------------------------------+
                              ▲
                              │  TDS Protocol
                              │  Port 1433
                              ▼
+-------------------------------------------------------------+
|             Microsoft SQL Server 2022 (Docker)              |
|          Image: mcr.microsoft.com/mssql/server:2022         |
|          (Bảng: Users, Conversations, Messages, ...)        |
+-------------------------------------------------------------+
```

---

## 5. Cấu trúc thư mục mã nguồn

```text
ChatSystem/
├── ChatServer/                     # Ứng dụng máy chủ (Console App, .NET 10)
│   ├── Configuration/              # ServerSettings
│   ├── Data/                       # ChatDbContext (Entity Framework Core)
│   ├── Models/                     # User, Conversation, Message, Attachment
│   ├── Networking/                 # Server, ClientConnection, ClientHandler (TCP Socket)
│   ├── Protocol/                   # NetworkMessage & MessageType
│   ├── Services/                   # AuthenticationService, MessageService, FileService
│   ├── Dockerfile                  # Cấu hình build container Linux .NET 10
│   └── appsettings.example.json    # File cấu hình mẫu
├── ChatClient/                     # Ứng dụng người dùng (WPF Desktop App, .NET 10)
│   ├── Assets/Emojis/              # Bộ icon cảm xúc dạng ảnh PNG
│   ├── Models/                     # ChatMessage model
│   ├── Services/                   # ChatClientService & EmojiHelper
│   ├── MainWindow.xaml (.cs)       # Giao diện chính (Dark Mode, Chat Room, Emoji Picker)
│   └── NewConversationDialog.cs    # Hộp thoại tạo cuộc trò chuyện mới
├── docker-compose.yml              # Thiết lập chạy nhanh ChatServer & SQL Server
├── .gitignore                      # Chặn rác build, cache IDE & appsettings.json
└── README.md                       # Tài liệu hướng dẫn
```

---

## 6. Các tính năng nổi bật

* 🔐 **Bảo mật xác thực:** Mật khẩu được mã hóa an toàn bằng thuật toán **SHA-256 kèm Salt ngẫu nhiên 16 bytes**, so sánh an toàn chống tấn công timing (`CryptographicOperations.FixedTimeEquals`).
* ⚡ **Giao tiếp Real-time hiệu năng cao:** Sử dụng Raw TCP Socket với cơ chế Framing dòng (`\n`), truyền nhận dữ liệu bất đồng bộ (`async/await`, `Dispatcher.Invoke`).
* 🎨 **Giao diện hiện đại (WPF Dark Mode):** Thiết kế bảng màu tối (`#0F0F13`) kết hợp sắc tím gradient hiện đại.
* 😀 **Biểu tượng cảm xúc phong phú:** Tích hợp bảng chọn Emoji theo danh mục (mặt cười, đồ ăn, hoạt động, biểu tượng).
* 🖼️ **Gửi hình ảnh & tập tin:** Hỗ trợ gửi ảnh xem trực tiếp trong bong bóng chat và gửi file đính kèm (giới hạn 10 MB/file).
* 🟢 **Theo dõi trạng thái Online/Offline:** Tự động cập nhật danh sách người dùng đang trực tuyến ngay lập tức khi có người đăng nhập hoặc ngắt kết nối.
* 📜 **Lịch sử tin nhắn:** Tự động lưu trữ vĩnh viễn trong SQL Server và phân trang tải lại lịch sử hội thoại.
