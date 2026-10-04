# RF4 Assistant Pro v2.3.33 — release checklist

## Исправление запуска

- [x] Упаковать релиз в отдельную папку внутри ZIP.
- [x] Добавить `START_RF4_ASSISTANT.cmd` со снятием блокировки файлов.
- [x] Добавить `START_DIAGNOSTICS.cmd` и host trace.
- [x] Добавить инструкцию `README_START.txt`.
- [x] Добавить обработку критической ошибки раннего WPF-запуска.
- [x] Добавить аварийный журнал в `%TEMP%`.
- [x] Продолжить нумерацию до `2.3.33`.

## Автоматическая проверка

- [x] Выполнить restore `win-x64`.
- [x] Выполнить Release build: 0 warnings, 0 errors.
- [x] Запустить `108/108` автоматических сценариев.
- [x] Собрать self-contained publish.
- [x] Проверить EXE, три ONNX-модели и запускаторы.
- [x] Проверить целостность ZIP и вложенную папку релиза.
- [x] Создать EXE ZIP, Source ZIP и SHA-256.

## Ручной Windows QA

- [ ] Распаковать ZIP через «Извлечь всё».
- [ ] Запустить `START_RF4_ASSISTANT.cmd`.
- [ ] Проверить создание `Logs/Events.log`.
- [ ] При сбое проверить `START_DIAGNOSTICS.cmd` и `Logs/HostStartup.log`.

`ReleaseReady=false` до подтверждения запуска на Windows.
