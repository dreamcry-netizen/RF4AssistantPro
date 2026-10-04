# RF4 Assistant Pro v2.3.35 — release artifacts

Дата подготовки: 2026-10-04  
SDK: `.NET 10.0.401`  
Платформа: `win-x64`  
Тесты: `109/109 PASS`

## Изменения

- capture state централизован в потокобезопасном Infrastructure coordinator;
- MainWindow не хранит mutable capture-флаги;
- режимы Space/V/M/C используют атомарные переходы;
- добавлен 109-й сценарий;
- сохранена single-file публикация, устраняющая зависимость от внешних RF4Assistant DLL.

## Автоматическая проверка

- restore/build/publish: `PASS`;
- build: `0 warnings`, `0 errors`;
- publish files: `10`; ONNX: `3`; PDB: `0`; LIB: `0`;
- ZIP integrity: `PASS`;
- SHA-256 EXE: `515ef88cb35ba33fb64566b705503bae7d5ad34c5d3309420c2fdeebe49874d6`;
- SHA-256 ZIP: `307b3e0268daff9355172ace5b5051cfe6732aa0f885ecc399d1aa5cdc198c2d`.

## Текущий статус

- запуск single-file v2.3.34 на Windows: `PASS`;
- функциональный Windows QA v2.3.35: `PENDING`;
- `ReleaseReady=false` до проверки Space/V/M/C.
