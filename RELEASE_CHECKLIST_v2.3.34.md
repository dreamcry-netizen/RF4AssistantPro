# RF4 Assistant Pro v2.3.34 — release checklist

## Single-file исправление запуска

- [x] Подтвердить причину по Windows Event Log: отсутствует `RF4Assistant.Infrastructure.dll`.
- [x] Перевести publish на единый self-contained EXE.
- [x] Встроить managed и native runtime-библиотеки внутрь EXE.
- [x] Оставить внешними только Assets, OCR-модели и запускаторы.
- [x] Удалить PDB и статические LIB из пакета.
- [x] Обновить инструкцию запуска.
- [x] Продолжить нумерацию до `2.3.34`.

## Автоматическая проверка

- [x] Выполнить restore `win-x64`.
- [x] Выполнить Release build: 0 warnings, 0 errors.
- [x] Запустить `108/108` автоматических сценариев.
- [x] Собрать single-file self-contained publish.
- [x] Проверить отсутствие внешних RF4Assistant DLL.
- [x] Проверить EXE и три ONNX-модели.
- [x] Проверить целостность ZIP.
- [x] Создать EXE ZIP, Source ZIP и SHA-256.

## Ручной Windows QA

- [ ] Полностью распаковать v2.3.34.
- [ ] Запустить `START_RF4_ASSISTANT.cmd`.
- [ ] Подтвердить появление главного окна и `Logs/Events.log`.

`ReleaseReady=false` до подтверждения запуска на Windows.
