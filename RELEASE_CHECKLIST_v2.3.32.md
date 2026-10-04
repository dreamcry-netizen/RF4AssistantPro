# RF4 Assistant Pro v2.3.32 — release checklist

## Разделение MainWindow

- [x] Вынести команды интерфейса в `MainWindow.Commands.cs`.
- [x] Вынести capture/OCR workflow в `MainWindow.Capture.cs`.
- [x] Оставить в `MainWindow.xaml.cs` создание окна, lifecycle и hotkeys.
- [x] Сохранить публичные методы, обработчики и поведение без изменений.
- [x] Продолжить нумерацию до `2.3.32`.
- [x] Обновить roadmap, changelog и Windows QA.

Текущее число зарегистрированных сценариев: `108`.

## Автоматическая проверка

- [x] Выполнить restore `win-x64`.
- [x] Выполнить Release build всех проектов.
- [x] Запустить `108/108` автоматических сценариев.
- [x] Собрать self-contained publish.
- [x] Проверить EXE и три ONNX-модели.
- [x] Создать EXE ZIP, Source ZIP и SHA-256.

## Ручной Windows QA

- [ ] Проверить привязку окна RF4 и ручной снимок улова.
- [ ] Проверить горячие клавиши Space, V, M и C.
- [ ] Проверить capture/import кафе и окна ручной проверки.
- [ ] Проверить серию садка и остановку повторным C.
- [ ] Повторить диагностику и screenshot retention.
- [ ] Проверить DPI 100%, 125% и 150%.

`ReleaseReady=false` до ручной Windows QA.

## Следующий этап

- [ ] Выделить независимый coordinator capture-workflow.
- [ ] Добавить Windows CI.
- [ ] Перенести тесты на xUnit или NUnit.
