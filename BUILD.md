# Сборка RF4AssistantPro v2.3.34

Требуется .NET 10 SDK на Windows.

Версия SDK зафиксирована в `global.json` как `10.0.401`.

При использовании .NET 10 SDK ссылки Windows SDK и `WinRT.Runtime`
подключаются SDK автоматически для Windows TFM. Локальные HintPath-ссылки и
ручной `WinRT.Runtime` PackageReference не требуются.

```powershell
dotnet restore .\RF4AssistantPro.sln -r win-x64
dotnet build .\RF4AssistantPro.sln -c Release
dotnet publish .\RF4AssistantPro\RF4AssistantPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
```

## Windows QA

Для воспроизводимой проверки этапа 18 используйте PowerShell-скрипт:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\qa\Run-WindowsQa.ps1 -LaunchInteractive
```

Скрипт проверяет наличие Windows и .NET 10 SDK, выполняет restore/build,
запускает интеграционные тесты, собирает self-contained `win-x64` publish и
проверяет наличие EXE и обязательного asset. Лог требований записывается в
`artifacts\windows-qa\logs\checklist-requirements.log`. Ручные проверки WPF, OCR,
keyboard hook, захвата окна и ZIP-экспорта перечислены в
`qa\WINDOWS_QA_CHECKLIST.md`.

Для повторного запуска без publish:

```powershell
.\qa\Run-WindowsQa.ps1 -SkipPublish
```
