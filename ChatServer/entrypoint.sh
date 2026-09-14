#!/bin/bash
set -m

echo "=========================================="
echo " Starting All-in-One Chat System"
echo " 1. Microsoft SQL Server 2022"
echo " 2. ChatServer (.NET 10)"
echo "=========================================="

# Khởi động SQL Server chạy nền
/opt/mssql/bin/sqlservr &
SQL_PID=$!

# Khởi động ChatServer
cd /app
./ChatServer &
APP_PID=$!

# Bắt tín hiệu dừng an toàn (Graceful shutdown)
trap "echo 'Stopping all services...'; kill -TERM $APP_PID $SQL_PID 2>/dev/null; wait" SIGTERM SIGINT

# Giữ container chạy cùng ChatServer
wait $APP_PID
