# Lab2 — Chat nhóm, emoji màu, ảnh và file lớn

Lab2 phát triển từ [Lab1](LAB1.md), giữ lại đăng ký, đăng nhập, chat 1-1 và lịch sử hội thoại; bổ sung các chức năng sau.

## Chức năng mở rộng

| Chức năng | Mô tả |
| --- | --- |
| Nhóm nhiều người | Tạo nhóm, chọn nhiều thành viên hoặc tạo nhóm trước rồi cho người khác tham gia sau. |
| Tham gia bằng mã nhóm | Chia sẻ ID hiển thị trong tên nhóm `#ID - Tên nhóm`; người đã đăng nhập nhập ID để tham gia. Thành viên được lưu kể cả khi offline. |
| Emoji màu | Chọn emoji theo chủ đề; emoji có tài nguyên PNG được hiển thị màu trong tin nhắn, emoji chưa có tài nguyên giữ nguyên Unicode. |
| Xem trước ảnh | Xem ảnh trước khi gửi, hiển thị thumbnail trong cửa sổ chat và lịch sử, mở ảnh lớn hơn và lưu ảnh gốc. Hỗ trợ PNG, JPEG, GIF và BMP. |
| Gửi tập tin lớn | Gửi và tải tập tin tối đa **500 MiB = 524.288.000 byte**, có hiển thị tiến độ và nút hủy. |
| Chat trong lúc truyền file | Giao diện vẫn sử dụng được khi upload hoặc download đang chạy. |
| Truyền đồng thời | Nhiều thành viên có thể tải file cùng lúc; mỗi kết nối hỗ trợ tối đa 3 upload đang hoạt động. |
| Kiểm tra toàn vẹn | Đối chiếu SHA-256 khi truyền xong; tải lỗi hoặc bị hủy không ghi đè tập tin đích đã có. |

## Xử lý bất đồng bộ và song song

- Mỗi kết nối được xử lý bằng task riêng; server gửi thông báo tới nhiều client bằng `Task.WhenAll`.
- File được đọc, ghi bất đồng bộ theo từng khối **64 KiB**, tránh nạp toàn bộ file 500 MiB vào bộ nhớ.
- Mỗi khối có xác nhận nhận dữ liệu; `RequestId` giúp ghép đúng phản hồi khi chat và truyền file xen kẽ.
- `SemaphoreSlim` bảo vệ thao tác ghi lên TCP, tránh trộn dữ liệu giữa các tác vụ.
- Thumbnail được tạo ngoài luồng giao diện và tải tối đa 3 lượt đồng thời.

## Lưu trữ và giới hạn

- SQL Server lưu tài khoản, nhóm, tin nhắn và thông tin tập tin; file mới lưu trong thư mục `uploads` trên server. Cần sao lưu cả hai nơi.
- Lịch sử và thông báo tin nhắn chứa thông tin tập tin; người nhận tải file gốc khi cần.
- Mã nhóm là ID chia sẻ, không phải mật khẩu; hội thoại 1-1 không cho tham gia bằng mã.
- GIF dùng ảnh tĩnh làm thumbnail. Truyền file chưa hỗ trợ tiếp tục sau mất kết nối; cần gửi lại.

## Demo chức năng

1. Mở ba client, đăng nhập bằng ba tài khoản khác nhau.
2. Tạo nhóm và cho các tài khoản còn lại tham gia bằng ID nhóm.
3. Gửi văn bản có emoji màu và ảnh có xem trước.
4. Gửi file 500 MiB, tiếp tục chat trong lúc gửi, rồi cho hai thành viên tải đồng thời.
5. Thử hủy một lượt truyền và mở lại hội thoại để kiểm tra lịch sử.

[Chức năng Lab1](LAB1.md) · [Hướng dẫn chạy và kiểm thử](../../README.md)
