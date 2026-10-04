# RF4 Assistant Pro v2.3.29 — release artifacts

Дата сборки: 2026-10-04  
Целевая платформа: `win-x64`  
Конфигурация: `Release`  
SDK: `.NET 10.0.401`  
Тесты: `106/106 PASS`

## Изменения

- исправлена очистка садка при применении изображений рыбы;
- добавлена безопасная замена `ObservableCollection`;
- исправлена OCR-форма `Пескарь обыкновённый`;
- зарегистрировано 106 автоматических сценариев.

## Текущий статус

- исходный архив: подготовлен;
- Linux cross-publish среда и каталоги кэша: подготовлены;
- .NET SDK 10.0.401 Linux x64: установлен и проверен;
- автоматические сценарии Core/Infrastructure: `106/106 PASS`;
- полный `win-x64` restore: `PASS`;
- Release build: `PASS`, 0 предупреждений, 0 ошибок;
- self-contained publish: `PASS`;
- `RF4AssistantPro.exe`: проверен как PE32+ GUI x86-64;
- три обязательные ONNX-модели: присутствуют;
- ZIP: проверен, ошибок сжатых данных нет;
- ручная Windows QA: `PENDING`;
- `ReleaseReady=false`.

## Артефакты

- `RF4AssistantPro_v2.3.29_win-x64.zip`
  - SHA-256:
    `454fb85a0f054ef0129354ee1f166c8a1787694bb1be4e2dc9fc3cc8edd18a6a`
- `RF4AssistantPro.exe`
  - SHA-256:
    `ed0790dbb2be07daad3a0f49edfc98174e612cf0d271e4cf626d8d34fa90fd66`

Отладочные PDB удалены из релизного пакета. Релиз остаётся незакрытым до
ручной проверки v2.3.29 на Windows.