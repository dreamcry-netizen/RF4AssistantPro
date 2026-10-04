# RF4 Assistant Pro v2.3.34 — разделение MainWindow

## Выполнено

- Команды интерфейса перенесены в partial-класс `MainWindow.Commands.cs`.
- Capture/OCR workflow перенесён в partial-класс `MainWindow.Capture.cs`.
- В `MainWindow.xaml.cs` оставлены зависимости, создание окна, lifecycle и hotkeys.
- Разделение выполнено без изменения публичных методов и пользовательского поведения.
- `MainWindow.xaml.cs` сокращён с 1190 до 320 строк.
- Выполнены `108/108` сценариев, Release build и self-contained publish: `0 warnings`, `0 errors`.

## Следующий этап

1. Выделить независимый coordinator capture-workflow из partial-класса.
2. Добавить Windows CI.
3. Перенести тесты на xUnit или NUnit.
4. Продолжить ручную Windows QA.
