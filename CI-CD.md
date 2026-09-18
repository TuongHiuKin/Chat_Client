# CI/CD Docker Hub

Workflow: [`.github/workflows/docker-hub.yml`](.github/workflows/docker-hub.yml).

## Thiết lập một lần

Repository image hiện tại: **tuongkien/chat-server**.

1. Trong Docker Hub, tạo Personal Access Token có quyền **Read & Write** cho tài khoản `tuongkien`.
2. Mở [GitHub → Settings → Secrets and variables → Actions](https://github.com/TuongHiuKin/Chat_Client/settings/secrets/actions).
3. Tạo repository secret tên chính xác **`DOCKERHUB_TOKEN`**, dán token vào ô secret. Không commit token, không đặt token vào YAML.
4. Mở [Actions](https://github.com/TuongHiuKin/Chat_Client/actions/workflows/docker-hub.yml), chọn lượt chạy mới nhất rồi **Re-run failed jobs**. Nếu lượt chạy đang kiểm thử, thêm secret trước khi job Publish bắt đầu.

Không cần secret username: workflow dùng `tuongkien` và image `tuongkien/chat-server` như project hiện tại. Khi thiếu token, bước Publish báo lỗi rõ ràng và không cập nhật Docker Hub.

## Khi nào workflow chạy?

- Push lên `main` hoặc `feature/docker`: build, test và cập nhật image Docker Hub.
- Pull request vào các nhánh trên: chỉ build/test, không đăng nhập hoặc push Docker Hub.
- Push tag `v2.0.0`, `v2.1.0`, ...: lưu image với tag phiên bản, không ghi đè các tag chạy mặc định.
- Có `workflow_dispatch` để chạy thủ công. Nút **Run workflow** hiển thị sau khi workflow được merge vào nhánh mặc định `main`; push vào `feature/docker` vẫn kích hoạt ngay trước khi merge.

Hai nhánh `main` và `feature/docker` đều được phép cập nhật tag chạy mặc định; nội dung tag là bản thuộc lượt publish thành công gần nhất. Khi chỉ muốn phát hành từ main, xóa `feature/docker` khỏi `push.branches` và điều kiện của job `publish`.

## Các bước CI/CD

1. Windows runner build .NET 10 server, WPF client và test harness; chạy test nhanh với SQL LocalDB và kiểm tra render emoji/WPF.
2. Hai Linux job build song song hai target Docker: `no-db` và `all-in-one`.
3. Mỗi image được khởi động thật, kết nối SQL, kiểm tra nhóm 3 người và upload/download file 1 MiB qua TCP, đối chiếu SHA-256.
4. Chỉ sau khi tất cả job kiểm thử thành công, đăng nhập Docker Hub và push hai image. Build cache giúp bước publish không phải compile lại toàn bộ.
5. Actions Summary ghi digest và lệnh pull đúng phiên bản. Workflow sử dụng action cố định theo commit SHA.

Kiểm thử CI dùng file nhỏ để chạy nhanh. Bộ test đầy đủ 500 MiB vẫn chạy được bằng `dotnet run --project Lab2.Tests` trên Windows có SQL Server.

## Tag trên Docker Hub

| Tag | Nội dung |
| --- | --- |
| `no-db` | Server Lab2, kết nối SQL Server bên ngoài |
| `latest` | Cùng bản với `no-db` |
| `all-in-one` | Server Lab2 kèm SQL Server 2022 |
| `no-db-sha-<full-commit-sha>` | Bản server không DB của một commit cụ thể |
| `all-in-one-sha-<full-commit-sha>` | Bản kèm DB của một commit cụ thể |
| `no-db-v2.0.0`, `all-in-one-v2.0.0` | Bản được phát hành từ git tag `v2.0.0` |

Tag theo commit/phiên bản giúp pull lại bản cũ khi cần; workflow không xóa các image trước đó. Docker Hub vẫn có thể cho phép ghi đè tag; digest trong Actions Summary là định danh chính xác của image đã publish.

## Cập nhật máy chạy Docker

Sau khi job Publish thành công, cập nhật stack hiện có bằng:

```powershell
docker compose pull chatserver
docker compose up -d --no-build chatserver
```

Lệnh trên tạo lại container app bằng image mới, giữ volume database `mssql_data` và tập tin `chat_uploads`. SQL container và cấu hình kết nối vẫn theo `docker-compose.yml` của bạn. Không dùng `down -v` khi muốn giữ dữ liệu.

Chỉ kéo image về:

```powershell
docker pull tuongkien/chat-server:no-db
docker pull tuongkien/chat-server:all-in-one
```

Chạy mới bản all-in-one (đặt mật khẩu đủ mạnh trước khi chạy):

```powershell
$env:MSSQL_SA_PASSWORD = 'THAY_BANG_MAT_KHAU_MANH_CUA_BAN'
docker run -d --name chat_all_in_one -p 5000:5000 -e MSSQL_SA_PASSWORD -v chat_all_sql:/var/opt/mssql -v chat_all_uploads:/app/uploads tuongkien/chat-server:all-in-one
```

Bản all-in-one mới yêu cầu `MSSQL_SA_PASSWORD` lúc chạy; image không chứa mật khẩu SQL mặc định. Khi tái sử dụng volume SQL cũ, truyền mật khẩu đang dùng của database đó.

Workflow chỉ xuất bản image lên Docker Hub; container đang chạy trên máy của bạn cần lệnh cập nhật bên trên. Kiến trúc hiện tại là **linux/amd64**, phù hợp image SQL Server đi kèm.

## Build/kiểm thử Docker thủ công

Từ folder `ChatSystem`, khi Docker Engine đang chạy:

```powershell
docker build -f ChatServer/Dockerfile --target no-db -t chat-server:test .
python scripts/docker_smoke.py --image chat-server:test --target no-db

docker build -f ChatServer/Dockerfile --target all-in-one -t chat-server:test .
python scripts/docker_smoke.py --image chat-server:test --target all-in-one
```

Docker build loại bỏ `appsettings.json` cục bộ, `.env`, thư mục uploads và output build. Image chỉ nhận cấu hình example không chứa secret; khi chạy, các biến môi trường ghi đè connection string và host/port.

Tham khảo chính thức: [Docker — test before push với GitHub Actions](https://docs.docker.com/build/ci/github-actions/test-before-push/).
