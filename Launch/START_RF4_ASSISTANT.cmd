@echo off
setlocal
cd /d "%~dp0"
if not exist "RF4AssistantPro.exe" (
  echo ERROR: RF4AssistantPro.exe not found in this folder.
  pause
  exit /b 2
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue" >nul 2>nul
start "RF4 Assistant Pro" "%~dp0RF4AssistantPro.exe"
