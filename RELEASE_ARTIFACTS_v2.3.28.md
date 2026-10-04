# RF4 Assistant Pro v2.3.28 — release artifacts

Дата сборки: 2026-10-04  
SDK: .NET 10.0.401  
Runtime: `win-x64`  
Конфигурация: `Release`  
Тесты: `104/104 PASS`

## Изменения

- Исправлено применение изображений рыбы в уловах и садке.
- Добавлен `FishImageResolver` в `RF4Assistant.Core`.
- Добавлена нормализация имён, псевдонимов и `file://`-путей.
- Добавлен регрессионный тест.

## Проверка

- `dotnet restore RF4AssistantPro.sln -r win-x64` — `PASS`
- `dotnet build RF4AssistantPro.sln -c Release --no-restore` — `PASS`
- автоматические тесты — `104/104 PASS`
- self-contained `win-x64` publish — `PASS`
- `RF4AssistantPro.exe` и OCR-модели в publish — `PASS`

## Пакеты

- `RF4AssistantPro_v2.3.28_win-x64.zip` — self-contained пакет запуска.
- `RF4AssistantPro_v2.3.28_Source.zip` — исходный код без `bin/obj`.
- SHA-256 сохранены в `SHA256SUMS.txt`.

Статус ручной Windows QA: `PENDING`  
Статус релиза: `ReleaseReady=false`