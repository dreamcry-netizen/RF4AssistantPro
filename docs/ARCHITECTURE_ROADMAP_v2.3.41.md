# RF4 Assistant Pro v2.3.41 — Windows CI

## Выполнено

- Добавлен workflow `.github/workflows/windows-release.yml`.
- Поддержаны запуск на `main`, pull request, тег `v*` и ручной запуск.
- Настроена установка закреплённой версии .NET SDK `10.0.401`.
- Автоматизированы restore, тестовый console runner и Release build.
- Автоматизирован self-contained single-file publish `win-x64`.
- CI проверяет EXE, три ONNX-модели и удаляет отладочные файлы.
- Создаются release ZIP и SHA-256.
- Артефакты публикуются через `actions/upload-artifact@v4`.
- Добавлена автоматическая проверка обязательных этапов workflow.
- Число зарегистрированных сценариев увеличено до `115`.
- Нумерация приложения и сценариев сборки продолжена до `2.3.41`.
- `115/115` локальных автоматических сценариев завершены успешно.
- Локальные Release build и self-contained publish завершены без ошибок и предупреждений.

## Следующий этап

1. Перевести тестовый console runner на стандартный test framework.
2. Добавить lock-файл NuGet для воспроизводимой сборки.
3. Закрепить GitHub Actions по commit SHA после первого прогона CI.
4. Выделить общий UI-компонент для OCR-словарей кафе и садка.
5. Продолжить декомпозицию крупных capture/OCR и Statistics UI-файлов.