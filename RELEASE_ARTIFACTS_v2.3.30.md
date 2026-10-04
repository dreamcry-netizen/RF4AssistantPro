# RF4 Assistant Pro v2.3.30 — release artifacts

Дата подготовки: 2026-10-04  
SDK: `.NET 10.0.401`  
Целевая платформа: `win-x64`  
Тесты: `107/107 PASS`

## Изменения

- добавлен coordinator применения импортированных данных;
- импорт backup/JSON и атомарное сохранение вынесены из ViewModel;
- каталог наживок и OCR-словарь включены в транзакцию;
- добавлена регрессия coordinator.

## Текущий статус

- тесты: `107/107 PASS`;
- restore `win-x64`: `PASS`;
- Release build: `PASS`, 0 предупреждений, 0 ошибок;
- self-contained publish: `PASS`;
- EXE: PE32+ GUI x86-64;
- три ONNX-модели: присутствуют;
- PDB в релизе: отсутствуют;
- ZIP: проверен, ошибок сжатых данных нет;
- ручная Windows QA: `PENDING`;
- `ReleaseReady=false`.

## Артефакты

- `RF4AssistantPro_v2.3.30_win-x64.zip`
  - SHA-256:
    `970c941092cae2c4aa6685db9f64995f8f3ea9aab29dbdfd2248ab3dda4a3853`
- `RF4AssistantPro.exe`
  - SHA-256:
    `e897080b923fb31a789772335b18b12fbce8d174676a89eacdefd7af1827bb8b`