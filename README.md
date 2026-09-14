# 💬 ChatSystem - Hệ Thống Chat Real-Time (.NET 10 & TCP Socket)

> **Đồ án môn học PRN222** - Ứng dụng chat đa người dùng kiến trúc Client - Server sử dụng Raw TCP Socket, định dạng gói tin JSON tùy biến, Entity Framework Core và giao diện WPF hiện đại.

---

## 📑 Mục lục
- [1. Kiến trúc hệ thống](#1-kiến-trúc-hệ-thống)
- [2. Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
- [3. Hướng dẫn chạy nhanh với Docker (Khuyên dùng)](#3-hướng-dẫn-chạy-nhanh-với-docker-khuyên-dùng)
- [4. Hướng dẫn chạy thủ công (Không dùng Docker)](#4-hướng-dẫn-chạy-thủ-công-không-dùng-docker)
- [5. Hướng dẫn sử dụng & Chat thử nghiệm](#5-hướng-dẫn-sử-dụng--chat-thử-nghiệm)
- [6. Các tính năng nổi bật](#6-các-tính-năng-nổi-bật)

---

## 1. Kiến trúc hệ thống

```
+-------------------------------------------------------------+
|                     ChatClient (WPF App)                    |
|             (Chạy trực tiếp trên máy người dùng)            |
+-------------------------------------------------------------+
                              ▲
                              │  TCP Socket / JSON (\n framed)
                              │  Port 5000
                              ▼
+-------------------------------------------------------------+
|                    ChatServer (.NET 10)                     |
|           - TcpListener lắng nghe kết nối đa luồng           |
|           - Broadcast tin nhắn theo thời gian thực          |
|           - Tự động đồng bộ schema CSDL với EF Core         |
+-------------------------------------------------------------+
                              ▲
                              │  TDS Protocol
                              │  Port 1433
                              ▼
+-------------------------------------------------------------+
|                  Microsoft SQL Server 2022                  |
|          (Bảng: Users, Conversations, Messages, ...)        |
+-------------------------------------------------------------+
```

---

## 2. Cấu trúc thư mục

```text
ChatSystem/
├── ChatServer/                     # Ứng dụng máy chủ (Console App, .NET 10)
│   ├── Configuration/              # Đọc cấu hình ServerSettings
│   ├── Data/                       # ChatDbContext (Entity Framework Core)
│   ├── Models/                     # Entity: User, Conversation, Message, Attachment
│   ├── Networking/                 # Server, ClientConnection, ClientHandler (TCP Socket)
│   ├── Protocol/                   # Định dạng gói tin NetworkMessage & MessageType
│   ├── Services/                   # Nghiệp vụ: Xác thực (Auth), Tin nhắn, File/Ảnh
│   ├── Dockerfile                  # Đóng gói ChatServer cho môi trường Linux
│   └── appsettings.example.json    # File cấu hình mẫu
├── ChatClient/                     # Ứng dụng người dùng (WPF Desktop App, .NET 10)
│   ├── Assets/Emojis/              # Kho icon cảm xúc dạng ảnh PNG
│   ├── Models/                     # ChatMessage model hiển thị trên UI
│   ├── Services/                   # ChatClientService (giao tiếp TCP ngầm) & EmojiHelper
│   ├── MainWindow.xaml (.cs)       # Giao diện chính (Dark Mode, Chat Room, Emoji Picker)
│   └── NewConversationDialog.cs    # Hộp thoại tạo cuộc trò chuyện mới
├── docker-compose.yml              # Quản lý chạy SQL Server & ChatServer tự động
├── .gitignore                      # Chặn rác build, cache IDE & appsettings.json
└── README.md                       # Tài liệu hướng dẫn
```

---

## 3. Hướng dẫn chạy nhanh với Docker (Khuyên dùng)

Cách này **không cần cài đặt SQL Server trên máy**, Docker sẽ tự động thiết lập toàn bộ backend và CSDL chỉ với 1 câu lệnh.

### Yêu cầu:
* Đã cài đặt và đang bật **Docker Desktop**.

### Các bước thực hiện:

#### Bước 1: Khởi động Server và CSDL
Mở Terminal / PowerShell tại thư mục `ChatSystem/` và chạy:
```bash
docker compose up -d --build
```
> Docker sẽ tự động:
> 1. Kéo image `mcr.microsoft.com/mssql/server:2022-latest` về và khởi động CSDL.
> 2. Build image cho `ChatServer`.
> 3. Tự động kết nối, tạo database `ChatDB` và các bảng.
> 4. Mở cổng `5000` để sẵn sàng nhận kết nối từ Client.

#### Bước 2: Kiểm tra trạng thái hệ thống
Xem log hoạt động của server:
```bash
docker compose logs -f chatserver
```
Khi thấy dòng:
```text
╔══════════════════════════════════════╗
║         CHAT SERVER STARTED          ║
╠══════════════════════════════════════╣
║  Host : 0.0.0.0                      ║
║  Port : 5000                         ║
╚══════════════════════════════════════╝
Waiting for clients...
```
Nghĩa là hệ thống đã sẵn sàng 100%!

#### Bước 3: Mở ứng dụng ChatClient
* Nhấp đúp chuột vào file thực thi `ChatClient.exe` (trong thư mục `ChatClient.exe/` hoặc `ChatClient/bin/Debug/net10.0-windows/ChatClient.exe`).
* Hoặc chạy từ source code:
  ```bash
  dotnet run --project ChatClient/ChatClient.csproj
  ```

#### Bước 4: Tắt hệ thống khi không sử dụng
```bash
docker compose down
```
*(Dữ liệu tin nhắn và tài khoản được lưu an toàn trong Docker Volume `mssql_data`, không bị mất khi tắt).*

---

## 4. Hướng dẫn chạy thủ công (Không dùng Docker)

Nếu muốn chạy trực tiếp bằng SQL Server cài trên máy tính:

### Bước 1: Cấu hình SQL Server
1. Mở `ChatServer/` $\rightarrow$ Copy file `appsettings.example.json` thành `appsettings.json`.
2. Điền chuỗi kết nối SQL Server của bạn vào:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.;Database=ChatDB;User Id=sa;Password=MẬT_KHẨU_CỦA_BẠN;TrustServerCertificate=True"
     },
     "ServerSettings": {
       "Host": "0.0.0.0",
       "Port": 5000
     }
   }
   ```
3. Đảm bảo dịch vụ `SQL Server` đang **Running** và giao thức **TCP/IP** đã được **Enabled** (cổng 1433).

### Bước 2: Khởi động Server
```bash
cd ChatServer
dotnet run
```
*Server sẽ tự động kết nối và tạo database `ChatDB` cùng toàn bộ bảng nếu chưa có.*

### Bước 3: Khởi động Client
```bash
cd ChatClient
dotnet run
```

---

## 5. Hướng dẫn sử dụng & Chat thử nghiệm

Để thử nghiệm tính năng chat thời gian thực giữa 2 người:

1. **Mở 2 cửa sổ Client riêng biệt:** Nhấp đúp mở 2 lần file `ChatClient.exe`.
2. **Cấu hình kết nối:**
   * **Host:** `127.0.0.1` (nếu chạy trên cùng máy) hoặc địa chỉ IP mạng LAN của máy chủ (ví dụ `192.168.1.x` nếu chạy khác máy).
   * **Port:** `5000`.
3. **Tạo tài khoản:**
   * Cửa sổ 1: Chọn tab **Đăng ký** $\rightarrow$ Tạo tài khoản `user1` (mật khẩu `123456`, Tên hiển thị `Alice`).
   * Cửa sổ 2: Chọn tab **Đăng ký** $\rightarrow$ Tạo tài khoản `user2` (mật khẩu `123456`, Tên hiển thị `Bob`).
4. **Bắt đầu trò chuyện:**
   * Ở cửa sổ của Alice, nhấn nút **+** (Tạo hội thoại mới) $\rightarrow$ Chọn **Bob** $\rightarrow$ Nhấn **Tạo**.
   * Nhập tin nhắn và trò chuyện!

---

## 6. Các tính năng nổi bật

* 🔐 **Bảo mật xác thực:** Mật khẩu được mã hóa an toàn bằng thuật toán **SHA-256 kèm Salt ngẫu nhiên 16 bytes**, so sánh an toàn chống tấn công timing (`CryptographicOperations.FixedTimeEquals`).
* ⚡ **Giao tiếp Real-time hiệu năng cao:** Sử dụng Raw TCP Socket với cơ chế Framing dòng (`\n`), truyền nhận dữ liệu bất đồng bộ không gây nghẽn UI (`async/await`, `Dispatcher.Invoke`).
* 🎨 **Giao diện hiện đại (WPF Dark Mode):** Thiết kế bảng màu tối (`#0F0F13`) kết hợp sắc tím gradient hiện đại.
* 😀 **Biểu tượng cảm xúc phong phú:** Tích hợp bảng chọn Emoji theo danh mục (mặt cười, đồ ăn, hoạt động, biểu tượng).
* 🖼️ **Gửi hình ảnh & tập tin:** Hỗ trợ gửi ảnh xem trực tiếp trong bong bóng chat và gửi file đính kèm (giới hạn 10 MB/file).
* 🟢 **Theo dõi trạng thái Online/Offline:** Tự động thông báo và cập nhật danh sách người dùng đang trực tuyến ngay lập tức khi có người đăng nhập hoặc thoát.
* 📜 **Lịch sử tin nhắn:** Hỗ trợ lưu trữ tin nhắn vĩnh viễn trong CSDL và phân trang tải lại lịch sử hội thoại khi người dùng đăng nhập.
