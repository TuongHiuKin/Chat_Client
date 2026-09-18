# ============================================================
# Stage 1: Build & Publish ChatServer (Self-contained Linux x64)
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ChatServer/ChatServer.csproj", "ChatServer/"]
RUN dotnet restore "ChatServer/ChatServer.csproj" -r linux-x64
COPY . .
WORKDIR "/src/ChatServer"
RUN dotnet publish "ChatServer.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

# ============================================================
# Stage 2: All-in-One Runtime (SQL Server 2022 + ChatServer)
# ============================================================
FROM mcr.microsoft.com/mssql/server:2022-latest AS final
USER root

# Copy ứng dụng ChatServer đã publish độc lập
WORKDIR /app
COPY --from=build /app/publish .
RUN chmod -R 755 /app && mkdir -p /app/uploads && chown mssql:root /app/uploads

# Copy kịch bản khởi động
COPY ChatServer/entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

# Cấu hình mặc định: SQL Server + ChatServer kết nối nội bộ qua localhost
ENV ACCEPT_EULA=Y \
    MSSQL_SA_PASSWORD=Password@12345 \
    MSSQL_PID=Developer \
    ConnectionStrings__DefaultConnection="Server=127.0.0.1,1433;Database=ChatDB;User Id=sa;Password=Password@12345;TrustServerCertificate=True" \
    ServerSettings__Host="0.0.0.0" \
    ServerSettings__Port="5000"

EXPOSE 5000 1433

USER mssql
ENTRYPOINT ["/entrypoint.sh"]