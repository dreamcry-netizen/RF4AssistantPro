# RF4 Assistant Pro v2.3.41 — release checklist

## Windows CI

- [x] Добавить workflow для Windows.
- [x] Поддержать push, pull request, tag и ручной запуск.
- [x] Закрепить .NET SDK `10.0.401`.
- [x] Автоматизировать restore и тесты.
- [x] Автоматизировать Release build и single-file publish.
- [x] Проверять EXE и три ONNX-модели.
- [x] Формировать ZIP и SHA-256.
- [x] Публиковать GitHub Actions artifact.
- [x] Добавить регрессию структуры workflow.
- [x] Продолжить нумерацию до `2.3.41`.

Текущее число зарегистрированных сценариев: `115`.

## Автоматическая проверка

- [x] Выполнить локальный restore `win-x64`.
- [x] Выполнить локальный Release build всех проектов.
- [x] Запустить `115/115` автоматических сценариев.
- [x] Собрать локальный self-contained publish.
- [x] Проверить EXE и три ONNX-модели.
- [x] Создать EXE ZIP, Source ZIP и SHA-256.
- [ ] Подтвердить первый успешный GitHub Actions run после публикации репозитория.

## Windows QA

- [ ] Запустить EXE из CI artifact на Windows.
- [ ] Проверить старт приложения и загрузку OCR-моделей.
- [ ] Проверить окно OCR-словаря садка.

`ReleaseReady=false` до первого запуска workflow в GitHub и ручной проверки EXE на Windows.