# RF4 Assistant Pro v2.3.33 — release artifacts

Дата подготовки: 2026-10-04  
SDK: ".NET 10.0.401"  
Целевая платформа: "win-x64"  
Тесты: "108/108 PASS"

## Исправление

- релиз распаковывается в одну отдельную папку;
- добавлен штатный запускатор со снятием Windows-блокировки;
- добавлен запускатор host-диагностики;
- ошибки ранней инициализации показываются пользователю и сохраняются в журнал.

## Проверка

- Release build: PASS, 0 warnings, 0 errors;
- self-contained publish: PASS;
- файлов: 500, ONNX: 3, PDB: 0;
- ZIP integrity: PASS;
- SHA-256 EXE: af752beddfb16c55ac3f0c573f4e5f0f7e264b76851faf9a2263251279a419be;
- SHA-256 EXE ZIP: ee1c308b4b6ed85ca041bfa3a20ee1f8e05da5dbc60e8cedd5734130923f2811.

Ручной Windows-запуск: PENDING.
