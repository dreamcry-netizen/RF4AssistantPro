# RF4 Assistant Pro v2.3.27 — release artifacts

Дата сборки: 2026-10-04  
SDK: .NET 10.0.401  
Runtime: `win-x64`  
Конфигурация: `Release`  
Тесты: `103/103 PASS`

## Проверка

- `dotnet restore RF4AssistantPro.sln -r win-x64` — PASS
- `dotnet build RF4AssistantPro.sln -c Release --no-restore` — PASS
- автоматические тесты — `103/103 PASS`
- self-contained `win-x64` publish — PASS
- EXE и OCR-модели в publish — PASS

Статус ручной Windows QA: `PENDING`  
Статус релиза: `ReleaseReady=false`

Запуск WPF-приложения, keyboard hook, захват RF4, Windows OCR и DPI-проверки
требуют Windows.