# RF4 Assistant Pro v2.3.26 — release artifacts

Дата сборки: 2026-10-04  
SDK: .NET 10.0.401  
Runtime: `win-x64`  
Конфигурация: `Release`  
Тип публикации: self-contained, `PublishSingleFile=false`

## Проверка

- `dotnet restore RF4AssistantPro.sln -r win-x64` — PASS
- `dotnet build RF4AssistantPro.sln -c Release --no-restore` — PASS
- автоматические тесты — `102/102 PASS`
- `dotnet publish RF4AssistantPro/RF4AssistantPro.csproj -c Release -r win-x64
  --self-contained true` — PASS
- `RF4AssistantPro.exe` и обязательные OCR-модели присутствуют в publish —
  PASS

## Ограничения

Сборка выполнена кросс-публикацией в Linux-окружении. Запуск WPF-приложения,
keyboard hook, захват RF4, Windows OCR и ручная QA-матрица требуют Windows и
не считаются автоматически подтверждёнными.

Статус ручной проверки: `PENDING`  
Статус релиза: `ReleaseReady=false`