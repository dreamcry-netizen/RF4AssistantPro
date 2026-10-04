# RF4 Assistant Pro v2.3.36 — release artifacts

Дата подготовки: 2026-10-04  
SDK: `.NET 10.0.401`  
Платформа: `win-x64`  
Тесты: `110/110 PASS`

## Исправление

- резервные записи садка открываются кнопкой «Исправить» и двойным щелчком;
- редактор показывает исходный screenshot;
- сохранение снимает `NeedsReview` и синхронизирует связанный улов;
- добавлен Core-планировщик и 110-й сценарий.

## Автоматическая проверка

- restore/build/publish: `PASS`;
- build: `0 warnings`, `0 errors`;
- publish files: `10`; ONNX: `3`; PDB: `0`; LIB: `0`;
- ZIP integrity: `PASS`;
- SHA-256 EXE: `9ae926e2d57fc727b1aa1d54ba6116665283cbebbe367e33e6169db793cbfd63`;
- SHA-256 ZIP: `ad10bdc797579aaed3defc7169dbb315d5c448ec898c3fe2fb3adff22321ce57`.

Ручная Windows-проверка редактора: `PENDING`.
