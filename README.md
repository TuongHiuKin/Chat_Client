# 💬 ChatSystem — Lab2 PRN222

Ứng dụng chat nhóm theo kiến trúc Client–Server, phát triển từ Lab1 bằng **.NET 10**, **WPF**, **TCP Socket**, **Entity Framework Core** và **SQL Server**.

Lab2 bổ sung emoji màu, gửi ảnh có xem trước, truyền file tới **500 MB** và cho phép nhiều người tham gia cùng một nhóm. Docker image Lab2 đã được build, kiểm thử và publish lên Docker Hub qua GitHub Actions.

[Docker Hub](https://hub.docker.com/r/tuongkien/chat-server/tags) · [GitHub Actions](https://github.com/TuongHiuKin/Chat_Client/actions/workflows/docker-hub.yml) · [Chi tiết Lab2](LAB2.md) · [Hướng dẫn CI/CD](CI-CD.md)

## Tính năng

- **Chat nhóm nhiều người:** tạo nhóm, chọn nhiều thành viên hoặc chia sẻ mã nhóm để người khác tham gia sau; vẫn hỗ trợ chat 1-1.
- **Emoji màu:** bảng chọn theo chủ đề, hiển thị emoji bằng ảnh màu trong tin nhắn.
- **Gửi ảnh:** xem trước trước khi gửi, hiển thị thumbnail trong khung chat và lịch sử, tải ảnh gốc về máy.
- **File lớn:** gửi/tải tối đa **500 MiB = 524.288.000 byte**, có tiến độ và nút hủy.
- **Bất đồng bộ và song song:** nhiều client hoạt động đồng thời, vẫn chat được khi truyền file; nhiều người có thể tải cùng lúc.
- **Kiểm tra dữ liệu:** đối chiếu SHA-256 khi hoàn tất; hủy/lỗi tải xuống không ghi đè file đích đang có.
- **Lưu lịch sử:** tài khoản, nhóm và tin nhắn lưu trong SQL; file mới lưu trên ổ đĩa server.

## Lấy mã nguồn Lab2

Mã Lab2 và workflow hiện nằm trên nhánh `feature/docker`:

```powershell
git clone --branch feature/docker https://github.com/TuongHiuKin/Chat_Client.git ChatSystem
cd ChatSystem
```

Các lệnh bên dưới chạy từ thư mục `ChatSystem`. Client cần **Windows**; Docker Server dùng **Linux containers, kiến trúc amd64**.

## Chạy server bằng Docker

Chọn một trong ba cách dưới đây. Cổng chat mặc định là **5000**.

### Cách 1 — Docker Compose: server và SQL ở hai container

Cần Docker Engine/Docker Desktop đang chạy. Compose dùng image `no-db` cho ứng dụng và SQL Server 2022 cho database:

```powershell
docker compose pull
docker compose up -d --no-build
docker compose logs -f chatserver
```

Compose hiện có cấu hình mật khẩu SQL dành cho demo. Nếu đổi mật khẩu, cập nhật đồng bộ `MSSQL_SA_PASSWORD`, câu lệnh healthcheck và connection string trong [docker-compose.yml](docker-compose.yml).

| Thành phần | Truy cập từ máy host | Volume lưu dữ liệu |
| --- | --- | --- |
| ChatServer | `localhost:5000` | `chat_uploads` |
| SQL Server | `localhost,14333` | `mssql_data` |

Để tự build server từ mã nguồn thay vì dùng image đã publish:

```powershell
docker compose up -d --build
```

### Cách 2 — All-in-one: server và SQL trong một container

Bản này không cần cài SQL Server riêng. Thay giá trị bên dưới bằng mật khẩu SQL đủ mạnh trước khi chạy:

```powershell
docker pull tuongkien/chat-server:all-in-one
$env:MSSQL_SA_PASSWORD = 'THAY_BANG_MAT_KHAU_MANH_CUA_BAN'
docker run -d --name chat_all_in_one -p 5000:5000 -e MSSQL_SA_PASSWORD -v chat_all_sql:/var/opt/mssql -v chat_all_uploads:/app/uploads tuongkien/chat-server:all-in-one
docker logs -f chat_all_in_one
```

Container chờ SQL Server sẵn sàng, tạo database `ChatDB` nếu chưa có, rồi mở server chat. Image yêu cầu mật khẩu qua biến môi trường; không chứa mật khẩu SQL mặc định. Khi dùng lại volume SQL cũ, truyền mật khẩu đang dùng của database đó.

### Cách 3 — No-db: kết nối SQL Server có sẵn

Ví dụ dưới đây dùng Docker Desktop, kết nối SQL Server trên máy host qua cổng TCP `1433`. Thay mật khẩu và cổng theo cấu hình thực tế:

```powershell
docker pull tuongkien/chat-server:no-db
$env:ConnectionStrings__DefaultConnection = 'Server=host.docker.internal,1433;Database=ChatDB;User Id=sa;Password=MAT_KHAU_SQL_CUA_BAN;TrustServerCertificate=True'
docker run -d --name chat_server -p 5000:5000 -e ConnectionStrings__DefaultConnection -v chat_uploads:/app/uploads tuongkien/chat-server:no-db
```

SQL Server cần cho phép kết nối TCP và tài khoản SQL tương ứng. Nếu dùng SQL từ Compose trên máy host, cổng được publish là `14333`.

## Chạy client Windows

Cài **.NET 10 SDK**, sau đó:

```powershell
dotnet run --project ChatClient
```

Trong màn hình kết nối:

- Server chạy cùng máy: nhập `localhost`, port `5000`.
- Server chạy trên máy khác trong LAN: nhập IP của máy đó, port `5000`; cho phép cổng này qua firewall của máy server.
- Đăng ký hoặc đăng nhập, rồi tạo/tham gia hội thoại.

Mở nhiều client bằng cách chạy `ChatClient/bin/Debug/net10.0-windows/ChatClient.exe` nhiều lần, mỗi cửa sổ dùng một tài khoản.

Muốn tạo bản chạy trên Windows không cần cài .NET:

```powershell
dotnet publish ChatClient -c Release -r win-x64 --self-contained true -o out/ChatClient
```

Chạy `out/ChatClient/ChatClient.exe` và chia sẻ **toàn bộ thư mục** `out/ChatClient`, bao gồm `Assets/Emojis`. Workflow hiện publish Docker Server; hãy build client từ mã Lab2 này để dùng đúng giao thức truyền file mới.

## Chạy server trực tiếp bằng .NET

Cần .NET 10 SDK và SQL Server. Nếu chưa có cấu hình cục bộ:

```powershell
if (-not (Test-Path ChatServer/appsettings.json)) {
    Copy-Item ChatServer/appsettings.example.json ChatServer/appsettings.json
}
```

Sửa `ConnectionStrings:DefaultConnection` trong `ChatServer/appsettings.json` cho phù hợp, rồi chạy:

```powershell
dotnet run --project ChatServer
```

Server sử dụng schema Lab1 hiện có; không cần xóa database để dùng Lab2. File mới mặc định lưu ở thư mục `uploads` cạnh executable server.

## Demo nhóm nhiều người và gửi file

1. Mở ít nhất ba client, đăng nhập ba tài khoản.
2. Chọn **Tạo hội thoại mới → Tạo nhóm**, nhập tên và chọn nhiều người online. Có thể tạo nhóm trước rồi mời sau.
3. Tên nhóm hiển thị `#ID - Tên nhóm`. Người khác chọn **Tham gia nhóm bằng mã** và nhập ID đó.
4. Gửi `Xin chào 😀 ❤️ 🎉` để kiểm tra emoji màu.
5. Bấm nút ảnh, chọn PNG/JPEG/GIF/BMP, xem trước rồi gửi. Thumbnail xuất hiện trong chat; nút tải lưu ảnh gốc.
6. Gửi file 500 MB và tiếp tục chat trong lúc upload. Hai người khác có thể bấm tải đồng thời.

Tạo file demo 500 MiB trên Windows:

```powershell
$demoFile = [System.IO.File]::Create((Join-Path $PWD 'demo-500MB.bin'))
try { $demoFile.SetLength(500MB) } finally { $demoFile.Dispose() }
```

## Cập nhật image mới từ Docker Hub

Với stack Compose đang chạy:

```powershell
docker compose pull chatserver
docker compose up -d --no-build chatserver
```

Lệnh trên cập nhật container ứng dụng, giữ dữ liệu trong các volume. Không dùng `docker compose down -v` nếu muốn giữ database và tập tin.

Với container tạo bằng `docker run`, `docker pull` chỉ tải image mới; cần tạo lại container ứng dụng với cùng cấu hình và volume để sử dụng image đó. Hướng dẫn cập nhật nằm trong [CI-CD.md](CI-CD.md).

## Docker Hub và CI/CD

Workflow [docker-hub.yml](.github/workflows/docker-hub.yml) thực hiện:

Workflow bắt đầu **sau khi một PR được merge vào `main`**. Các bước đều dùng đúng mã nguồn của commit vừa merge:

1. Build server, WPF client và test harness trên Windows; chạy test SQL/TCP và render giao diện.
2. Build hai Docker target trên Linux, khởi động container thật, kiểm tra nhóm ba người và truyền file có SHA-256.
3. Khi tất cả kiểm thử đạt, đăng nhập bằng secret `DOCKERHUB_TOKEN` và publish lên `tuongkien/chat-server`.

| Docker tag | Nội dung |
| --- | --- |
| `no-db` | ChatServer, dùng SQL Server bên ngoài |
| `latest` | Cùng bản với `no-db` |
| `all-in-one` | ChatServer kèm SQL Server 2022 |

Luồng sử dụng: **push nhánh làm việc → tạo PR vào `main` → review và merge → build/test → push Docker Hub**. Push nhánh, mở/cập nhật PR, đóng PR không merge và push git tag không chạy build/test/publish. Push trực tiếp vào `main` cũng không kích hoạt workflow này.

Mỗi lần thành công, pipeline cập nhật ba tag `no-db`, `all-in-one`, `latest`; không tạo thêm tag theo commit hoặc phiên bản. Nếu test thất bại, bước publish không chạy và các image đang có trên Docker Hub được giữ nguyên. Máy đang chạy server cần thực hiện lệnh cập nhật container ở trên.

Secret Docker Hub đã được cấu hình cho repository này. Khi fork sang repository khác, thiết lập token và tài khoản theo [hướng dẫn CI/CD](CI-CD.md).

## Kiến trúc và xử lý bất đồng bộ

```text
ChatClient (Windows / WPF)
        │ TCP :5000 — JSON theo dòng
        ▼
ChatServer (.NET 10)
        ├── SQL Server: tài khoản, nhóm, tin nhắn, metadata tập tin
        └── uploads/: file gốc, checksum, thumbnail
```

Mỗi kết nối có task và `DbContext` riêng; broadcast dùng `Task.WhenAll`. File được đọc/ghi bất đồng bộ theo khối **64 KiB**, có ACK và `RequestId`; `SemaphoreSlim` ngăn các tác vụ ghi trộn dữ liệu lên TCP. Thumbnail được tạo ngoài luồng UI và tải tối đa ba lượt đồng thời.

Lịch sử/broadcast chứa metadata; người nhận chỉ tải file gốc khi yêu cầu. Khi sao lưu, cần giữ **cả SQL database và thư mục/volume uploads**. Chi tiết triển khai và giới hạn nằm trong [LAB2.md](LAB2.md).

## Kiểm thử

Trên Windows có SQL Server:

```powershell
dotnet run --project Lab2.Tests -- --quick # Kiểm thử nhanh với file 2 MiB
dotnet run --project Lab2.Tests           # File 500 MiB, hai lượt tải đồng thời
dotnet run --project Lab2.Tests -- --ui   # Render WPF và kiểm tra emoji màu
```

Test SQL mặc định dùng Windows Authentication tới `localhost`; có thể ghi đè bằng `LAB2_TEST_SQL`. Bộ test tạo rồi xóa database thử nghiệm riêng, không sửa database ứng dụng; tài khoản SQL cần quyền tạo/xóa database.

Đã kiểm thử thành công file 500 MiB với hai người tải đồng thời và SHA-256 khớp dữ liệu gốc. CI Docker kiểm tra cả hai image bằng file 1 MiB; xem kết quả từng lượt chạy tại [GitHub Actions](https://github.com/TuongHiuKin/Chat_Client/actions/workflows/docker-hub.yml).
