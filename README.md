# 💬 ChatSystem - Hệ Thống Chat Real-Time (.NET 10 & TCP Socket)

> **Đồ án môn học PRN222** - Ứng dụng chat đa người dùng kiến trúc Client - Server sử dụng Raw TCP Socket, định dạng gói tin JSON tùy biến, Entity Framework Core và giao diện WPF hiện đại.

---

## 💻 1. Tải Ứng Dụng Client (ChatClient.exe)

Ứng dụng Client chạy trực tiếp trên Windows (Self-contained, không cần cài đặt .NET SDK hay Visual Studio):

* 📥 [👉 **Bấm vào đây để tải `ChatClient.exe` (GitHub Releases)**](https://github.com/TuongHiuKin/Chat_Client/releases/latest/download/ChatClient.exe)
* 📦 Hoặc xem danh sách các phiên bản tại: [GitHub Releases Page](https://github.com/TuongHiuKin/Chat_Client/releases)

---

## 🐳 2. Tải & Chạy Server từ Docker Hub

Tùy theo nhu cầu sử dụng, dự án cung cấp **2 loại Docker Image riêng biệt** được lưu trữ tại [Docker Hub Repository: `tuongkien/chat-server`](https://hub.docker.com/repository/docker/tuongkien/chat-server):

### ⭐ Lựa chọn 1: Image Có Sẵn CSDL (All-in-One - Khuyên dùng)
> **Mô tả:** Tích hợp sẵn cả **ChatServer (.NET 10)** và **Microsoft SQL Server 2022** trong cùng 1 container duy nhất. Không cần cài đặt CSDL hay bất cứ file nào khác trên máy.

* **Lệnh kéo image về máy:**
  ```bash
  docker pull tuongkien/chat-server:all-in-one
  ```

* **Lệnh chạy ngay lập tức (Chỉ đúng 1 câu lệnh):**
  ```bash
  docker run -d -p 5000:5000 --name chat_all_in_one tuongkien/chat-server:all-in-one
  ```
  *(Container tự động khởi chạy SQL Server, tự kết nối và tạo cơ sở dữ liệu `ChatDB` cùng toàn bộ bảng, sau đó mở cổng `5000` sẵn sàng cho Client kết nối!)*

---

### 🚀 Lựa chọn 2: Image Không Có DB (Standalone - Nhẹ ~112 MB)
> **Mô tả:** Chỉ chứa ứng dụng **ChatServer**, dành cho ai muốn kết nối trực tiếp đến **SQL Server đã cài sẵn trên máy tính** hoặc chạy qua **Docker Compose**.

* **Lệnh kéo image về máy:**
  ```bash
  docker pull tuongkien/chat-server:no-db
  ```

* **Cách A - Kết nối với SQL Server đang chạy trên máy tính (Host):**
  ```bash
  docker run -d -p 5000:5000 --name chat_server -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=ChatDB;User Id=sa;Password=MẬT_KHẨU_SQL_CỦA_BẠN;TrustServerCertificate=True" tuongkien/chat-server:no-db
  ```
  *(💡 **Lưu ý:** Hãy thay `MẬT_KHẨU_SQL_CỦA_BẠN` bằng mật khẩu tài khoản `sa` của SQL Server trên máy bạn. Server sẽ tự động kết nối và khởi tạo database `ChatDB` cùng toàn bộ cấu trúc bảng nếu chưa tồn tại).*

* **Cách B - Chạy kết hợp với SQL Server container qua `docker compose`:**
  ```bash
  docker compose up -d
  ```

---

## 🏗️ 3. Kiến Trúc & Tính Năng Nổi Bật

### Sơ đồ luồng dữ liệu:
```text
[ ChatClient (WPF App) ]
           ▲
           │ TCP Socket (Port 5000, JSON \n framed)
           ▼
[ ChatServer (.NET 10) ]
           ▲
           │ TDS Protocol (Port 1433)
           ▼
[ Microsoft SQL Server 2022 (ChatDB) ]
```

### Các tính năng chính:
* 🔐 **Bảo mật xác thực:** Mật khẩu được băm an toàn bằng thuật toán **SHA-256 kèm Salt ngẫu nhiên 16 bytes**, so sánh an toàn chống timing attack.
* ⚡ **Giao tiếp Real-time đa luồng:** Xử lý kết nối TCP bất đồng bộ bằng `TcpListener`, `ConcurrentDictionary` và hàng đợi tin nhắn thread-safe.
* 🎨 **Giao diện hiện đại:** WPF Dark Mode tông màu tím gradient (`#7C6EFF`), danh sách người dùng online cập nhật tức thì.
* 😀 **Biểu tượng cảm xúc phong phú:** Tích hợp bảng chọn Emoji phân loại theo chủ đề.
* 🖼️ **Truyền nhận tệp tin & hình ảnh:** Xem trực tiếp ảnh trong tin nhắn và tải tài liệu đính kèm (giới hạn tối đa 10 MB/tệp).
* 📜 **Lưu trữ & Lịch sử:** Toàn bộ tin nhắn được lưu vĩnh viễn trong CSDL và tự động phân trang khi tải lại.
