# CI/CD Docker Hub

Workflow: [`.github/workflows/docker-hub.yml`](.github/workflows/docker-hub.yml).

## Thiết lập một lần

Repository image hiện tại: **tuongkien/chat-server**.

1. Trong Docker Hub, tạo Personal Access Token có quyền **Read & Write** cho tài khoản `tuongkien`.
2. Mở [GitHub → Settings → Secrets and variables → Actions](https://github.com/TuongHiuKin/Chat_Client/settings/secrets/actions).
3. Tạo repository secret tên chính xác **`DOCKERHUB_TOKEN`**, dán token vào ô secret. Không commit token, không đặt token vào YAML.
4. Merge PR vào `main` để bắt đầu pipeline. Nếu một lượt chạy sau merge bị lỗi do thiếu token, thêm secret rồi mở [Actions](https://github.com/TuongHiuKin/Chat_Client/actions/workflows/docker-hub.yml), chọn lượt chạy đó và **Re-run failed jobs**.

Không cần secret username: workflow dùng `tuongkien` và image `tuongkien/chat-server` như project hiện tại. Khi thiếu token, bước Publish báo lỗi rõ ràng và không cập nhật Docker Hub.

## Khi nào workflow chạy?

Pipeline chỉ thực hiện build/test/publish **sau khi PR được merge vào `main`**:

```text
Push code lên nhánh làm việc
            ↓
Tạo PR vào main → Review → Merge PR
                              ↓
             Build và kiểm thử mã đã merge
                              ↓
                 Tất cả kiểm thử thành công
                              ↓
           Cập nhật image trên Docker Hub
```

Workflow dùng sự kiện `pull_request` với `types: [closed]`, lọc nhánh đích `main`, và điều kiện `github.event.pull_request.merged == true`. Mỗi job checkout `merge_commit_sha` để kiểm thử và publish đúng mã đã merge.

- Push nhánh, mở/cập nhật PR: chưa chạy workflow.
- Đóng PR mà không merge: các job bị bỏ qua, không publish.
- Merge PR vào `main`: chạy build/test, rồi publish khi thành công.
- Push trực tiếp vào `main`, push git tag: không kích hoạt workflow.
- Không có nút chạy mới thủ công (`workflow_dispatch`); có thể chạy lại một lượt sau merge bị lỗi bằng **Re-run failed jobs**.

Test chạy sau merge theo quy trình này. Nếu test thất bại, code đã ở `main` nhưng image Docker Hub chưa được cập nhật; sửa lỗi bằng PR tiếp theo hoặc chạy lại nếu là lỗi tạm thời.

## Các bước CI/CD

1. Windows runner build .NET 10 server, WPF client và test harness; chạy test nhanh với SQL LocalDB và kiểm tra render emoji/WPF.
2. Hai Linux job build song song hai target Docker: `no-db` và `all-in-one`.
3. Mỗi image được khởi động thật, kết nối SQL, kiểm tra nhóm 3 người và upload/download file 1 MiB qua TCP, đối chiếu SHA-256.
4. Chỉ sau khi tất cả job kiểm thử thành công, đăng nhập Docker Hub và push hai image. Build cache giúp bước publish không phải compile lại toàn bộ.
5. Actions Summary ghi digest và lệnh pull image mới. Workflow sử dụng action cố định theo commit SHA để giữ phiên bản công cụ ổn định; đây không phải tag Docker image.

Kiểm thử CI dùng file nhỏ để chạy nhanh. Bộ test đầy đủ 500 MiB vẫn chạy được bằng `dotnet run --project Lab2.Tests` trên Windows có SQL Server.

## Tag trên Docker Hub

| Tag | Nội dung |
| --- | --- |
| `no-db` | Server Lab2, kết nối SQL Server bên ngoài |
| `latest` | Cùng bản với `no-db` |
| `all-in-one` | Server Lab2 kèm SQL Server 2022 |

Mỗi lần publish thành công sẽ cập nhật ba tag cố định ở trên. Không tạo thêm tag theo commit hoặc git tag phiên bản. Digest trong Actions Summary vẫn xác định chính xác nội dung image của lượt publish đó.

Các tag theo commit đã publish trước khi đổi quy trình vẫn nằm trên Docker Hub; workflow mới không tạo thêm hoặc tự xóa chúng.

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

Tham khảo chính thức: [Docker — test before push với GitHub Actions](https://docs.docker.com/build/ci/github-actions/test-before-push/), [GitHub — chạy workflow khi PR được merge](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#running-your-pull_request-workflow-when-a-pull-request-merges).
