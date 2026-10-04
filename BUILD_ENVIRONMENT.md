# Среда сборки RF4 Assistant Pro v2.3.34

## Windows

Предпочтительный путь — Windows 10/11 x64 с .NET 10 SDK:

```cmd
BUILD_WINDOWS_EXE.cmd
```

Сценарий выполняет restore, тесты, self-contained publish `win-x64`,
удаление отладочных PDB, упаковку ZIP и создание SHA-256.

## Linux cross-publish

Поддерживается сборка Windows EXE из Linux:

```bash
chmod +x BUILD_LINUX_WIN_X64.sh
./BUILD_LINUX_WIN_X64.sh
```

Сценарий ожидает .NET SDK `10.0.401` в файле:

```text
/data/rf4-build-env/cache/dotnet-sdk-10.0.401-linux-x64.tar.gz
```

Путь можно изменить:

```bash
RF4_DOTNET_SDK_ARCHIVE=/path/to/sdk.tar.gz ./BUILD_LINUX_WIN_X64.sh
```

NuGet-кэш хранится отдельно:

```text
/data/rf4-build-env/nuget-packages
```

Для первого restore требуется доступ к NuGet либо заранее заполненный
кэш со всеми прямыми и транзитивными пакетами. После успешного restore
повторная сборка может выполняться офлайн.

По умолчанию Linux-сценарий работает в офлайн-режиме: недоступные NuGet
источники игнорируются, аудит пакетов не запрашивается. Для сетевого restore:

```bash
RF4_OFFLINE=0 ./BUILD_LINUX_WIN_X64.sh
```

Результаты:

```text
dist/RF4AssistantPro_v2.3.34_win-x64/RF4AssistantPro.exe
dist/RF4AssistantPro_v2.3.34_win-x64.zip
dist/RF4AssistantPro_v2.3.34_win-x64.zip.sha256
```