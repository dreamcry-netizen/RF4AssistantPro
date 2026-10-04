# RF4 Assistant Pro v2.3.30 — coordinator применения импорта

## Выполнено

- Создан `ImportedDataApplyCoordinator` в Infrastructure.
- Импортированный backup нормализуется и атомарно сохраняется вне ViewModel.
- JSON-импорт использует тот же coordinator и сохраняет связанные разделы.
- Каталог наживок и OCR-словарь участвуют в общей транзакции.
- Coordinator возвращает готовый снимок для обновления UI-коллекций.
- Добавлена автоматическая регрессия; `107/107 PASS`.
- Выполнены restore, Release build и self-contained `win-x64` publish.

## Оставшиеся ограничения

- Выбор файлов всё ещё выполняется через диалоги ViewModel.
- Диагностика и screenshot retention остаются в основном ViewModel.
- Команды и capture-workflow главного окна ещё не разделены.

## Следующий этап

1. Вынести диагностику и screenshot retention.
2. Разделить команды и workflow `MainWindow.xaml.cs`.
3. Добавить Windows CI.
4. Продолжить ручную Windows QA.