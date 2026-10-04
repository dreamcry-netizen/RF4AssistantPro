# RF4 Assistant Pro v2.3.31 — release artifacts

Дата подготовки: 2026-10-04  
SDK: `.NET 10.0.401`  
Целевая платформа: `win-x64`  
Тесты: `108/108 PASS`

## Изменения

- добавлен coordinator диагностики и screenshot retention;
- MainWindow и ViewModel больше не координируют инфраструктурные операции;
- добавлена регрессия сохранения настроек и защиты связанных PNG.

## Автоматическая проверка

- restore `win-x64`: `PASS`;
- Release build: `PASS`, `0 warnings`, `0 errors`;
- self-contained publish: `PASS`;
- EXE: `PE32+ GUI x86-64`;
- состав publish: `497` файлов, `3` ONNX-модели, `0` PDB;
- проверка ZIP: `PASS`;
- SHA-256 EXE: `e25e38915cc22275fe98021dddf1b3c32967e866872211e05548d70aeef2ecc8`;
- SHA-256 EXE ZIP: `dd38cdfbea3c6c2a1939b85986418dba66630ec42f89eb6092b351b838f59b5b`.

## Текущий статус

- автоматические проверки: `PASS`;
- ручная Windows QA: `PENDING`;
- `ReleaseReady=false` до завершения ручной Windows QA.
