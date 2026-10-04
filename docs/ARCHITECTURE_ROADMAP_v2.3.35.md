# RF4 Assistant Pro v2.3.35 — capture workflow coordinator

## Выполнено

- Создан `CaptureWorkflowCoordinator` в Infrastructure.
- Состояния capture busy, V/M armed и keepnet series централизованы.
- Переходы защищены lock и выполняются атомарно.
- MainWindow оставляет только UI, keyboard hook и вызов платформенных сервисов.
- Добавлена автоматическая регрессия переходов coordinator; `109/109 PASS`.
- Выполнены build и single-file publish: `0 warnings`, `0 errors`.
- Подтверждён успешный Windows-запуск single-file v2.3.34.

## Следующий этап

1. Вынести orchestration OCR/capture из partial-класса в специализированные workflow.
2. Выполнить функциональный Windows QA Space/V/M/C.
3. Добавить Windows CI.
4. Перенести test runner на xUnit или NUnit.
