# RF4 Assistant Pro v2.3.34 — диагностика и retention

## Выполнено

- Создан `DiagnosticsRetentionCoordinator` в Infrastructure.
- Сбор диагностической сводки и экспорт ZIP централизованы.
- Настройки хранения, summary, ручная и автоматическая очистка используют
  один coordinator.
- ViewModel отвечает только за UI-статусы и вычисление защищённых путей.
- MainWindow больше не вызывает `DiagnosticExportService` напрямую.
- Добавлена автоматическая регрессия; `108/108 PASS`.
- Выполнены restore, Release build и self-contained publish: `0 warnings`, `0 errors`.
- Проверены Windows EXE, три ONNX-модели и целостность ZIP.

## Следующий этап

1. Разделить команды и capture-workflow `MainWindow.xaml.cs`.
2. Добавить Windows CI.
3. Перенести тесты на xUnit или NUnit.
4. Продолжить ручную Windows QA.