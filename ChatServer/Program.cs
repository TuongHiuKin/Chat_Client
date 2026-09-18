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
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? configuration.GetConnectionString("DefaultConnection")
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
    const int maxRetries = 10;
    bool ready = false;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Console.WriteLine($"[Attempt {attempt}/{maxRetries}] Connecting to SQL Server and ensuring database schema...");
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("SQL SERVER CONNECTED & Database schema ensured!");
            ready = true;
            break;
        }
        catch (Exception ex)
        {
            if (attempt == maxRetries)
            {
                Console.WriteLine($"SQL SERVER CONNECTION FAILED after {maxRetries} attempts: {ex.Message}");
                if (!Console.IsInputRedirected)
                {
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                }
                return;
            }

            Console.WriteLine($"Waiting for SQL Server ({ex.Message}). Retrying in 3 seconds...");
            await Task.Delay(3000);
        }
    }

    if (!ready) return;
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

serverSettings.Host = Environment.GetEnvironmentVariable("ServerSettings__Host") ?? serverSettings.Host;
if (int.TryParse(Environment.GetEnvironmentVariable("ServerSettings__Port"), out int configuredPort)) serverSettings.Port = configuredPort;
var server = new ChatServerHost(
    serverSettings.Host,
    serverSettings.Port,
    dbOptions);

await server.StartAsync(cts.Token);