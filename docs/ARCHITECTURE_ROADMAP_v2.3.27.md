# RF4 Assistant Pro v2.3.27 — data transfer слой

## Продолжение в v2.3.28

- Логика сопоставления изображения рыбы вынесена в Core
  (`FishImageResolver`).
- UI больше не содержит собственного алгоритма сравнения имени рыбы.
- Добавлены нормализация имён, псевдонимов и `file://`-путей.
- Добавлен регрессионный тест для сценария с изображением карточки рыбы.
- После Windows-проверки нужно обновить статус релиза и закрыть ручной QA.

## Выполнено

- Backup/import/export координируются через
  `RF4Assistant.Infrastructure/Services/DataTransferService.cs`.
- `StatisticsViewModel` больше не вызывает `Rf4BackupService` и
  `CatchRecordStore` напрямую для пользовательского импорта/экспорта.
- Composition root создаёт один transfer-сервис из общих storage/catalog
  зависимостей.
- Добавлен тест JSON round-trip через новый сервис.
- Атомарное применение импортированного состояния и существующий
  `PersistState` сохранены в ViewModel до следующего этапа.

## Ограничения

Этот этап уменьшает связанность, но не завершает разделение ViewModel:

- UI-диалоги остаются в ViewModel;
- применение загруженного backup к ObservableCollection остаётся в ViewModel;
- диагностика и screenshot retention ещё не вынесены;
- ручная Windows QA всё ещё не завершена.

## Следующий этап

1. Выделить coordinator применения импортированных данных.
2. Вынести диагностику и screenshot retention.
3. Разделить команды `MainWindow.xaml.cs`.
4. Добавить Windows CI и повторить полный release preflight.