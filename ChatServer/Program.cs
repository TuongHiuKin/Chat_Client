// ============================================================
// ChatServer - Entry Point
// ============================================================
// Các bước khởi động:
//   1. Đọc cấu hình từ appsettings.json (Host, Port, ConnectionString)
//   2. Kiểm tra kết nối SQL Server và tự động migrate DB nếu cần
//   3. Khởi động ChatServer (TcpListener) và chờ Client kết nối
//   4. Bắt Ctrl+C để tắt Server sạch sẽ (Graceful shutdown)
// ============================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using ChatServer.Configuration;
using ChatServer.Data;
// Alias để tránh xung đột tên giữa namespace 'ChatServer' và class 'ChatServer'
using ChatServerHost = ChatServer.Networking.ChatServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;


// ── 1. Đọc cấu hình từ appsettings.json ─────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: true)
    .Build();

var connectionString =
    configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionString 'DefaultConnection' is missing in appsettings.json.");

var serverSettings =
    configuration
        .GetSection("ServerSettings")
        .Get<ServerSettings>()
    ?? throw new InvalidOperationException(
        "ServerSettings section is missing in appsettings.json.");

// ── 2. Kiểm tra kết nối SQL Server ──────────────────────────────────────────
Console.WriteLine("Checking SQL Server connection...");

var dbOptions = new DbContextOptionsBuilder<ChatDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using (var db = new ChatDbContext(dbOptions))
{
    bool connected = await db.Database.CanConnectAsync();

    if (!connected)
    {
        Console.WriteLine("SQL SERVER CONNECTION FAILED! Check appsettings.json.");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine("SQL SERVER CONNECTED!");

    // Tự động tạo / cập nhật schema DB nếu chưa tồn tại
    // Dùng EnsureCreated thay cho Migration trong giai đoạn phát triển
    await db.Database.EnsureCreatedAsync();
    Console.WriteLine("Database schema ensured.");
}

// ── 3. Khởi động ChatServer ──────────────────────────────────────────────────
using var cts = new CancellationTokenSource();

// Bắt sự kiện Ctrl+C để tắt Server sạch sẽ
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Không kill process ngay, để cleanup hoàn thành
    Console.WriteLine("\nShutdown signal received...");
    cts.Cancel();
};

var server = new ChatServerHost(
    serverSettings.Host,
    serverSettings.Port,
    dbOptions);

await server.StartAsync(cts.Token);