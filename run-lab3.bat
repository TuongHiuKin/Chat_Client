@echo off
title Lab 3 - SignalR Web Chat
cd /d "%~dp0Lab3"
echo ==============================================================
echo              KHOI DONG LAB 3 (SIGNALR WEB CHAT)
echo ==============================================================
for /f "tokens=*" %%a in ('powershell -NoProfile -Command "(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback|vEthernet' -and $_.IPAddress -notlike '169.254*' } | Select-Object -First 1).IPAddress"') do set MY_LAN_IP=%%a
echo  [1] Truy cap tren may nay:
echo      http://localhost:5038
echo.
if defined MY_LAN_IP (
    echo  [2] May khac cung mang LAN / Wi-Fi truy cap vao:
    echo      http://%MY_LAN_IP%:5038
)
echo ==============================================================
echo  Luu y: Neu may khac khong truy cap duoc, vui long chuot phai
echo         vao file allow-firewall.bat va chon 'Run as administrator'.
echo ==============================================================
dotnet run
pause
