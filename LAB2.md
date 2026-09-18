# Lab2 — Chat nhóm, emoji màu, ảnh và file 500 MB

## Chạy trên Windows

Yêu cầu .NET 10 SDK và SQL Server. Giữ connection string đang dùng ở `ChatServer/appsettings.json`; không cần đổi schema hay xóa dữ liệu Lab1.

Từ folder `ChatSystem`, mở hai terminal:

```powershell
dotnet run --project ChatServer
dotnet run --project ChatClient
```

Mở thêm nhiều client bằng cách chạy `ChatClient/bin/Debug/net10.0-windows/ChatClient.exe` nhiều lần. Khi chạy qua LAN, các máy nhập IP của máy server và cổng 5000; server cần lắng nghe `0.0.0.0` và được cho phép qua Windows Firewall.

**Phải chạy cả server và client từ mã nguồn Lab2 này.** Các bản EXE/Docker image Lab1 đã phát hành chưa có giao thức truyền file mới.

## Demo

1. Đăng ký/đăng nhập ba tài khoản trên ba client.
2. Bấm **Tạo hội thoại mới → Tạo nhóm**, nhập tên và chọn nhiều người online. Có thể tạo nhóm một mình trước.
3. Tên nhóm hiển thị `#ID - Tên nhóm`. Chia sẻ ID; người khác chọn **Tham gia nhóm bằng mã** và nhập ID để vào. Nhóm không đặt giới hạn hai người; thành viên offline vẫn được lưu. Chat 1-1 không cho tham gia bằng mã.
4. Chọn emoji trong bảng chọn, gửi `Xin chào 😀 ❤️ 🎉`. Emoji có sẵn hiển thị bằng PNG màu cả trong bảng chọn và bong bóng chat; chuỗi emoji chưa có asset giữ nguyên dạng Unicode.
5. Bấm nút ảnh, chọn PNG/JPEG/GIF/BMP, xem trước rồi **Gửi ảnh**. Thumbnail xuất hiện trong chat và lịch sử. Bấm thumbnail để mở lớn hơn; **Save original** hoặc nút tải lưu ảnh gốc. GIF dùng ảnh tĩnh cho thumbnail.
6. Bấm nút tập tin để gửi file tới **500 MiB = 524.288.000 byte** (bao gồm file 500 MB). Cửa sổ tiến độ có nút **Hủy**; vẫn có thể gửi tin nhắn trong lúc truyền. Có thể mở nhiều lượt truyền, tối đa 3 upload đang hoạt động mỗi kết nối.
7. Hai thành viên khác bấm tải cùng lúc. Chỉ khi tải đủ và đúng SHA-256, file mới thay thế đường dẫn đã chọn. Hủy/lỗi không ghi đè file cũ.

Tạo nhanh file demo (chạy tại thư mục muốn lưu):

```powershell
$demoFile = [System.IO.File]::Create((Join-Path $PWD 'demo-500MB.bin'))
try { $demoFile.SetLength(500MB) } finally { $demoFile.Dispose() }
```

## Cách áp dụng bất đồng bộ và song song

- `Server.cs`: mỗi kết nối có task và `ChatDbContext` riêng. Các client được phục vụ đồng thời; broadcast dùng `Task.WhenAll`.
- `Shared/JsonLineReader.cs`: đọc JSON theo dòng bằng buffer có giới hạn. Gói gửi lên tối đa 512 Ki ký tự; client nhận tối đa 4 Mi ký tự.
- `FileTransfers.cs`: `ReadAsync`/`WriteAsync`, chia file thành **64 KiB/khối**, chờ ACK từng khối để tránh hàng đợi dữ liệu tăng không giới hạn. `RequestId` ghép đúng phản hồi khi chat, upload và download xen kẽ.
- `SemaphoreSlim` bảo vệ ghi TCP để các tác vụ không trộn byte. Không gửi cả file thành một chuỗi Base64; chỉ mã hóa từng khối.
- `FileService.cs`: stream vào `.part`, kiểm tra thứ tự, dung lượng và SHA-256, rồi lưu metadata SQL và chuyển sang file hoàn chỉnh. Hủy hoặc ngắt kết nối sẽ xóa upload dở.
- `MainWindow.Transfers.cs`: tạo thumbnail bằng `Task.Run`; lấy preview tối đa 3 lượt đồng thời; giao diện cập nhật bằng `await`/`Progress<T>`.
- Lịch sử và broadcast chỉ chứa metadata. Thumbnail riêng tối đa 256 KiB; file gốc chỉ tải theo yêu cầu. Không giữ file 500 MB trong model giao diện hay cột SQL.

## Lưu trữ và cấu hình

File mới nằm trong `uploads` cạnh executable server. SQL vẫn dùng bảng `Attachments` cũ; `FileData` rỗng cho file mới, dữ liệu ở `<attachmentId>.bin`, kèm `.sha256` và `.preview` nếu là ảnh. Cần sao lưu **cả database và folder uploads**.

Có thể đặt biến môi trường:

- `CHAT_STORAGE_PATH`: thư mục lưu file, phải có quyền ghi.
- `ConnectionStrings__DefaultConnection`: ghi đè kết nối SQL.
- `ServerSettings__Host`, `ServerSettings__Port`: địa chỉ/cổng server.

`docker-compose.yml` đã thêm volume `chat_uploads`. Dùng `docker compose up --build -d` để build mã mới; không dùng image cũ cho client Lab2. File Lab1 trong SQL vẫn tải được; ảnh Lab1 lớn không có thumbnail riêng sẽ hiện thẻ tải về.

Mã nhóm là ID chia sẻ, không phải mã bí mật: tài khoản đã đăng nhập biết ID có thể tham gia nhóm. Truyền file chưa hỗ trợ resume sau mất kết nối; cần gửi lại. TCP giữ cơ chế Lab1, chưa có TLS.

## Kiểm thử tích hợp

```powershell
dotnet run --project Lab2.Tests -- --quick # 2 MiB, chạy nhanh
dotnet run --project Lab2.Tests           # 500 MiB, hai download đồng thời
```

Mặc định kết nối SQL Server `localhost` bằng Windows Authentication. Nếu cần, đặt `LAB2_TEST_SQL` thành connection string của SQL Server thử nghiệm. Bộ test tạo database `ChatLab2Test_<GUID>` và thư mục `.lab2-tests/<GUID>`, sau đó tự xóa; không sửa database ứng dụng. Tài khoản SQL cần quyền tạo/xóa database. Bài test 500 MiB cần khoảng 2,5 GiB trống ở thời điểm cao nhất.

Các kiểm tra: ba thành viên vào cùng nhóm, join đồng thời và join lặp, Unicode emoji, chat trong lúc upload, hai download đồng thời với SHA-256, metadata lịch sử, ảnh thumbnail, file rỗng, hủy upload/download và dọn `.part`, giữ file đích cũ, giới hạn 500 MiB ở cả client/server, khối sai thứ tự, checksum sai, quyền gửi/tải và từ chối join chat 1-1.

### Kết quả đã chạy trên máy hiện tại

- Build server, WPF client và test harness: 0 lỗi, 0 cảnh báo.
- Kiểm thử thực tế 500 MiB: upload và hai download đồng thời thành công, SHA-256 trùng nhau; khoảng 104 giây trên localhost trong lượt chạy này.
- Peak working set khoảng 172 MiB cho tiến trình kiểm thử chứa cả server và các client; đây là số đo của lượt test, không phải giới hạn RAM cam kết.
- WPF smoke test: kiểm tra inline emoji màu, tạo dialog nhóm và render template ảnh/file thành công (`dotnet run --project Lab2.Tests -- --ui`).
