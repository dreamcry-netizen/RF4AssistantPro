@echo off
setlocal
cd /d "%~dp0"
if not exist "Logs" mkdir "Logs"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue" >nul 2>nul
set "COREHOST_TRACE=1"
set "COREHOST_TRACE_VERBOSITY=4"
set "COREHOST_TRACEFILE=%~dp0Logs\HostStartup.log"
echo Starting RF4 Assistant Pro with host diagnostics...
start "RF4 Assistant Pro diagnostics" /wait "%~dp0RF4AssistantPro.exe"
set "CODE=%ERRORLEVEL%"
echo.
echo Exit code: %CODE%
echo Diagnostic file: %~dp0Logs\HostStartup.log
if not "%CODE%"=="0" pause
exit /b %CODE%
