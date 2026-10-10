@echo off
title Mo cong Tuong lua (Windows Firewall) cho Lab 3 va Lab 2
echo ==============================================================
echo         MO CONG TUONG LUA (WINDOWS FIREWALL)
echo ==============================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Vui long chay file nay bang cach:
    echo     Chuot phai vao file -> Chon 'Run as administrator'
    echo ==============================================================
    pause
    exit /b 1
)

echo Dang mo cong 5038 (Lab 3 SignalR Web)...
netsh advfirewall firewall add rule name="Lab3_SignalR_5038" dir=in action=allow protocol=TCP localport=5038

echo Dang mo cong 5000 (Lab 2 ChatServer)...
netsh advfirewall firewall add rule name="ChatServer_5000" dir=in action=allow protocol=TCP localport=5000

echo ==============================================================
echo [OK] DA MO CONG THANH CONG!
echo Cac may khac cung mang LAN/Wi-Fi gio day da co the truy cap duoc.
echo ==============================================================
pause
