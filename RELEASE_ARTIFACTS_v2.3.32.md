# RF4 Assistant Pro v2.3.32 — release artifacts

Дата подготовки: 2026-10-04  
SDK: `.NET 10.0.401`  
Целевая платформа: `win-x64`  
Тесты: `108/108 PASS`

## Изменения

- команды интерфейса вынесены в `MainWindow.Commands.cs`;
- capture/OCR workflow вынесен в `MainWindow.Capture.cs`;
- основной `MainWindow.xaml.cs` сокращён с 1190 до 320 строк;
- публичные точки входа и поведение сохранены.

## Автоматическая проверка

- restore `win-x64`: `PASS`;
- Release build: `PASS`, `0 warnings`, `0 errors`;
- self-contained publish: `PASS`;
- EXE: `PE32+ GUI x86-64`;
- состав publish: `497` файлов, `3` ONNX-модели, `0` PDB;
- проверка ZIP: `PASS`;
- SHA-256 EXE: `50cb79bd4dfdee97c80e3c6ca659ee31a3ef5f7bcb4e3670b25d8263b25e7609`;
- SHA-256 EXE ZIP: `150b28f6ec46dd976364507110aca810948a3f34dc092bd16ec4f7d7a036bbdd`.

## Текущий статус

- автоматические проверки: `PASS`;
- ручная Windows QA: `PENDING`;
- `ReleaseReady=false` до завершения ручной Windows QA.
