# Lab1 — Ứng dụng chat Client–Server

Lab1 xây dựng nền tảng chat bằng **WPF**, **TCP Socket**, **.NET** và **SQL Server**. Server tiếp nhận nhiều client kết nối đồng thời.

## Chức năng

| Chức năng | Mô tả |
| --- | --- |
| Kết nối server | Client nhập địa chỉ và cổng của server để kết nối. |
| Đăng ký, đăng nhập | Quản lý tài khoản; mật khẩu được lưu dưới dạng hash kèm salt. |
| Danh sách online | Hiển thị người dùng đang trực tuyến để bắt đầu hội thoại. |
| Chat 1-1 | Tạo hội thoại và gửi, nhận tin nhắn văn bản theo thời gian thực. |
| Lịch sử hội thoại | Lưu tin nhắn trong SQL Server; tải lịch sử theo từng trang 50 tin nhắn. |
| Emoji | Có bảng chọn emoji và bộ tài nguyên ảnh emoji. |
| Ảnh và tập tin cơ bản | Hỗ trợ gửi ảnh, tập tin tối đa 10 MiB; dữ liệu tập tin được lưu trong SQL Server. |

## Nền tảng kỹ thuật

- **Client:** WPF, giao diện đăng nhập và cửa sổ chat.
- **Server:** xử lý kết nối TCP, xác thực và chuyển tiếp tin nhắn giữa các client.
- **Giao thức:** thông điệp JSON phân cách bằng dòng mới.
- **Dữ liệu:** Entity Framework Core và SQL Server lưu tài khoản, hội thoại, thành viên và tin nhắn.

Lab1 có mô hình dữ liệu hội thoại nhóm; giao diện tạo và tham gia nhóm nhiều người được hoàn thiện ở Lab2. Lab2 cũng mở rộng khả năng hiển thị emoji, xem trước ảnh và truyền tập tin lớn.

[Chức năng Lab2](LAB2.md) · [Hướng dẫn chạy dự án](../../README.md)
