# RF4 Assistant Pro v2.3.27 — release checklist

## Этап v2.3.27 — выделение data transfer слоя

- [x] Добавить `DataTransferService` в Infrastructure.
- [x] Убрать прямую координацию backup/import/export из ViewModel.
- [x] Сохранить атомарное сохранение состояния в `PersistState`.
- [x] Сохранить совместимость `.rf4backup` и JSON-импорта уловов.
- [x] Добавить регрессионный тест JSON round-trip через transfer-сервис.
- [x] Продолжить нумерацию версии до `2.3.27`.

## Автоматическая проверка

- [x] Выполнить `dotnet restore` с .NET 10 SDK для `win-x64`.
- [x] Выполнить Release build всех проектов.
- [x] Запустить все автоматические сценарии: `103/103 PASS`.
- [x] Собрать self-contained `win-x64` publish.
- [x] Проверить EXE и OCR-модели в publish.

Фактическое число автоматических сценариев: `103`; успешно `103`, ошибок
`0`.

## Ручная Windows QA

Результаты smoke-прогона из `qa/QA_EVIDENCE_v2.3.26_2026-10-04.md`
переносятся без изменения. По-прежнему не закрыты:

- серия садка;
- кафе с 8 и 9 карточками;
- backup/restore с реальными PNG;
- CSV и диагностический ZIP;
- закрытие/повторный запуск;
- DPI 100%, 125% и 150%.

`ReleaseReady` остаётся `false` до прохождения ручной матрицы.

## Следующий этап

- [ ] Вынести применение импортированных данных из ViewModel в отдельный
  coordinator.
- [ ] Вынести диагностику и screenshot retention.
- [ ] Разделить `MainWindow.xaml.cs` на команды и workflow-сервисы.
- [ ] Добавить Windows CI.