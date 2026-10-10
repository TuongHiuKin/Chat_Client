@echo off
title Chat Server
cd /d "%~dp0"
echo ==============================================
echo              STARTING CHAT SERVER
echo ==============================================
dotnet run --project ChatServer
pause
