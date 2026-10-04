# RF4 Assistant Pro v2.3.34 — release artifacts

Дата подготовки: 2026-10-04  
SDK: .NET 10.0.401  
Платформа: win-x64  
Тесты: 108/108 PASS

## Исправление

- причина v2.3.33: отсутствующая внешняя RF4Assistant.Infrastructure.dll;
- v2.3.34 опубликована как единый self-contained EXE;
- managed и native зависимости встроены;
- внешними оставлены OCR-модели, Assets и запускаторы.

## Проверка

- Release build: PASS, 0 warnings, 0 errors;
- publish files: 10; ONNX: 3; PDB: 0; LIB: 0;
- внешние RF4Assistant.Core/Infrastructure/Windows DLL: отсутствуют;
- SHA-256 EXE: f200f3b91ccf79847d3265aea9183281e6faff7123101c3b04128e59f82479f9;
- SHA-256 ZIP: 543fb58d0137f70d9b93316e68ef7d49f798ce2b258197df2cfdc6e102d0f35e.

Ручной Windows-запуск: PENDING.
