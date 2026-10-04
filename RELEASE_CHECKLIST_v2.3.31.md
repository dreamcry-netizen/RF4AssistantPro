# RF4 Assistant Pro v2.3.31 — release checklist

## Диагностика и screenshot retention

- [x] Добавить `DiagnosticsRetentionCoordinator`.
- [x] Вынести сбор диагностической сводки из ViewModel.
- [x] Вынести retention settings и storage summary.
- [x] Вынести ручную и автоматическую очистку снимков.
- [x] Перевести диагностический экспорт MainWindow на coordinator.
- [x] Сохранить защиту PNG, связанных с пользовательскими данными.
- [x] Добавить регрессионный тест retention.
- [x] Продолжить нумерацию до `2.3.31`.
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

- [ ] Повторить обязательные регрессии v2.3.29–v2.3.30.
- [ ] Сохранить и повторно открыть retention settings.
- [ ] Проверить ручную очистку старых незащищённых PNG.
- [ ] Проверить автоматическую очистку после capture.
- [ ] Создать диагностический ZIP без снимков и со снимками.
- [ ] Проверить DPI 100%, 125% и 150%.

`ReleaseReady=false` до ручной Windows QA.

## Следующий этап

- [ ] Разделить команды и capture-workflow `MainWindow.xaml.cs`.
- [ ] Добавить Windows CI.
- [ ] Перенести тесты на xUnit или NUnit.