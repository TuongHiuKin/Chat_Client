# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ChatServer/ChatServer.csproj ChatServer/
RUN dotnet restore ChatServer/ChatServer.csproj -r linux-x64
COPY ChatServer/ ChatServer/
COPY Shared/ Shared/
RUN dotnet publish ChatServer/ChatServer.csproj -c Release -r linux-x64 \
    --self-contained true --no-restore -o /app/publish \
    && cp ChatServer/appsettings.example.json /app/publish/appsettings.json

# Use this target with an external SQL Server (including docker compose).
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS no-db
WORKDIR /app
COPY --from=build /app/publish/ ./
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads
ENV ServerSettings__Host=0.0.0.0 \
    ServerSettings__Port=5000 \
    CHAT_STORAGE_PATH=/app/uploads
EXPOSE 5000
USER app
ENTRYPOINT ["./ChatServer"]

# Keep all-in-one as the default target for existing docker build commands.
FROM mcr.microsoft.com/mssql/server:2022-latest AS all-in-one
USER root
WORKDIR /app
COPY --from=build /app/publish/ ./
COPY ChatServer/entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh \
    && mkdir -p /app/uploads && chown mssql:root /app/uploads
ENV ACCEPT_EULA=Y \
    MSSQL_PID=Developer \
    ServerSettings__Host=0.0.0.0 \
    ServerSettings__Port=5000 \
    CHAT_STORAGE_PATH=/app/uploads
EXPOSE 5000
USER mssql
ENTRYPOINT ["/entrypoint.sh"]
