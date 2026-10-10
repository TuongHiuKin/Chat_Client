@echo off
title Chat Client
cd /d "%~dp0"
echo ==============================================
echo              STARTING CHAT CLIENT
echo ==============================================
dotnet run --project ChatClient
pause
