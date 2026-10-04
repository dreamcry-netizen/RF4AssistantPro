@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "VERSION=2.3.41"
set "PROJECT=RF4AssistantPro\RF4AssistantPro.csproj"
set "TEST_PROJECT=RF4AssistantPro.Tests\RF4AssistantPro.Tests.csproj"
set "DIST=dist\RF4AssistantPro_v%VERSION%_win-x64"
set "ZIP=dist\RF4AssistantPro_v%VERSION%_win-x64.zip"

echo ============================================================
echo  RF4 Assistant Pro v%VERSION% - Windows x64 build
echo ============================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo ERROR: .NET 10 SDK is not installed.
    echo Download: https://dotnet.microsoft.com/download/dotnet/10.0
    echo Install the x64 SDK and run this file again.
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('dotnet --version') do set "DOTNET_VERSION=%%v"
echo Found .NET SDK: %DOTNET_VERSION%
if not "%DOTNET_VERSION:~0,3%"=="10." (
    echo.
    echo ERROR: .NET 10 SDK is required.
    pause
    exit /b 1
)

echo.
echo [1/4] Restore...
dotnet restore RF4AssistantPro.sln -r win-x64
if errorlevel 1 goto :failed

echo.
echo [2/4] Integration tests...
dotnet run --project "%TEST_PROJECT%" -c Release --no-restore
if errorlevel 1 goto :failed

echo.
echo [3/4] Self-contained win-x64 publish...
if exist "%DIST%" rmdir /s /q "%DIST%"
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "%DIST%"
if errorlevel 1 goto :failed

if not exist "%DIST%\RF4AssistantPro.exe" (
    echo ERROR: RF4AssistantPro.exe was not created.
    goto :failed
)
del /s /q "%DIST%\*.pdb" >nul 2>nul
del /s /q "%DIST%\*.lib" >nul 2>nul

copy /y "Launch\START_RF4_ASSISTANT.cmd" "%DIST%\" >nul
copy /y "Launch\START_DIAGNOSTICS.cmd" "%DIST%\" >nul
copy /y "Launch\README_START.txt" "%DIST%\" >nul

echo.
echo [4/4] ZIP and SHA256...
if exist "%ZIP%" del /q "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%DIST%' -DestinationPath '%ZIP%' -CompressionLevel Optimal"
if errorlevel 1 goto :failed
powershell -NoProfile -ExecutionPolicy Bypass -Command "$h=(Get-FileHash '%ZIP%' -Algorithm SHA256).Hash.ToLower(); Set-Content -Encoding ascii '%ZIP%.sha256' ($h + '  ' + [IO.Path]::GetFileName('%ZIP%'))"
if errorlevel 1 goto :failed

echo.
echo ============================================================
echo  BUILD COMPLETED
echo  EXE: %DIST%\RF4AssistantPro.exe
echo  ZIP: %ZIP%
echo ============================================================
explorer "%CD%\dist"
pause
exit /b 0

:failed
echo.
echo ============================================================
echo  BUILD FAILED. Review the messages above.
echo ============================================================
pause
exit /b 1
