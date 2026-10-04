# PaddleOCR / ONNX

С версии 2.3.0 OCR кафе использует локальный PaddleOCR через
`RapidOCRSharpOnnx` и `Microsoft.ML.OnnxRuntime`.

Основной режим:

- PP-OCRv5;
- кириллическая recognition-модель;
- CPU inference;
- работа без интернета после установки;
- снимки не отправляются во внешние сервисы.

В `RF4AssistantPro/Assets/Ocr/Paddle/` включены:

- `ch_PP-OCRv5_det_mobile.onnx` — детекция текста;
- `cyrillic_PP-OCRv5_rec_mobile.onnx` — распознавание кириллицы;
- `ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx` — ориентация строк.

Если ONNX-модель отсутствует или не загружается, приложение пишет
предупреждение и использует прежний `Windows.Media.Ocr`.