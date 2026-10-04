# RF4 Assistant Pro v2.3.35 — release checklist

## Capture workflow coordinator

- [x] Добавить потокобезопасный `CaptureWorkflowCoordinator`.
- [x] Вынести общий флаг capture in progress.
- [x] Вынести одноразовые состояния V и M.
- [x] Вынести arming и lifecycle серии садка C.
- [x] Подключить coordinator через composition root.
- [x] Удалить capture-флаги из `MainWindow`.
- [x] Добавить регрессионный тест атомарных переходов.
- [x] Продолжить нумерацию до `2.3.35`.

Текущее число зарегистрированных сценариев: `109`.

## Автоматическая проверка

- [x] Выполнить restore `win-x64`.
- [x] Выполнить Release build всех проектов.
- [x] Запустить `109/109` автоматических сценариев.
- [x] Собрать single-file self-contained publish.
- [x] Проверить EXE и три ONNX-модели.
- [x] Создать EXE ZIP, Source ZIP и SHA-256.

## Windows QA

- [x] Подтверждён запуск single-file v2.3.34: два запуска, два штатных выхода `0`.
- [ ] Проверить Space и защиту от повторного capture.
- [ ] Проверить одноразовые V и M.
- [ ] Проверить старт/остановку серии садка C.
- [ ] Проверить кафе и импорт PNG.

`ReleaseReady=false` до функционального Windows QA.
