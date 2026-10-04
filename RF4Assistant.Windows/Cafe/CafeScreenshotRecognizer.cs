using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RF4AssistantPro.Services;
using RF4AssistantPro.Fish;
using WinBitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;

namespace RF4AssistantPro.Cafe;

public sealed class CafeScreenshotRecognizer
{
    private readonly PaddleOcrService _paddleOcr = new();
    private readonly CafeOcrAliasStore _ocrAliasStore = new();

    private static readonly Regex WeightRegex = new(
        @"Масса\s+от\s+(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>г|кг)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PriceRegex = new(
        @"^\d+[.,]\d{1,2}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex NameRegex = new(
        @"^[A-Za-zА-ЯЁа-яё][A-Za-zА-ЯЁа-яё\-\s]{2,}$",
        RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<CafeOffer>> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = await file.OpenReadAsync();
        var decoder = await WinBitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        OcrEngine? engine = null;
        var usedPaddle = false;
        IReadOnlyList<OcrLine> lines;
        try
        {
            var paddleLines = await _paddleOcr.RecognizeAsync(
                imagePath,
                cancellationToken);
            lines = CafeOcrTextLayout.ComposeLines(
                    paddleLines.Select(line => new CafeOcrBlock(
                        line.Text,
                        line.Left,
                        line.Top,
                        line.Right,
                        line.Bottom)))
                .Select(line => new OcrLine(
                    line.Text,
                    line.CenterX,
                    line.CenterY))
                .ToList();
            usedPaddle = lines.Count > 0;
            if (usedPaddle)
            {
                AppLog.Info(
                    $"PaddleOCR кафе распознал строк: {lines.Count}.");
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"PaddleOCR кафе недоступен, используется Windows OCR: " +
                $"{exception.Message}");
            lines = [];
        }

        if (!usedPaddle)
        {
            engine = TryCreateRussianEngine()
                ?? throw new InvalidOperationException(
                    "Не удалось инициализировать PaddleOCR и Windows OCR.");
            var result = await engine.RecognizeAsync(bitmap);
            cancellationToken.ThrowIfCancellationRequested();
            lines = result.Lines
                .Select(line =>
                {
                    var wordRects = line.Words
                        .Select(word => word.BoundingRect)
                        .ToList();

                    if (wordRects.Count == 0)
                    {
                        return new OcrLine(line.Text, 0, 0);
                    }

                    var left = wordRects.Min(rect => rect.X);
                    var top = wordRects.Min(rect => rect.Y);
                    var right = wordRects.Max(rect => rect.X + rect.Width);
                    var bottom = wordRects.Max(rect => rect.Y + rect.Height);
                    return new OcrLine(
                        line.Text,
                        left + (right - left) / 2,
                        top + (bottom - top) / 2);
                })
                .ToList();
        }
        else
        {
            // Общий PaddleOCR может распознать текст, но потерять название
            // отдельной карточки. Если Windows OCR доступен, используем его
            // для повторной проверки фиксированных crop-областей даже после
            // успешного общего PaddleOCR.
            engine = TryCreateRussianEngine();
            if (engine is not null)
            {
                AppLog.Info(
                    "Для PaddleOCR включена дополнительная проверка " +
                    "отдельных карточек через Windows OCR.");
            }
        }

        AppLog.OcrDetails(
            "OCR кафе, строки: " +
            string.Join(" | ", lines.Select(line => line.Text)));

        var slots = ParseCardSlots(
            lines,
            decoder.PixelWidth,
            decoder.PixelHeight);
        IReadOnlyList<RecognizedCardFields> verifiedFields;
        if (usedPaddle)
        {
            var paddleFields = await RecognizePaddleCardFieldsAsync(
                imagePath,
                decoder.PixelWidth,
                decoder.PixelHeight,
                cancellationToken);
            var windowsFields = engine is null
                ? null
                : await RecognizeCardFieldsAsync(
                    imagePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    engine,
                    cancellationToken);
            verifiedFields = MergeCardFields(
                paddleFields,
                windowsFields);
        }
        else
        {
            verifiedFields = engine is null
                ? Enumerable
                    .Repeat(new RecognizedCardFields(null, null, null), 10)
                    .ToList()
                : await RecognizeCardFieldsAsync(
                    imagePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    engine,
                    cancellationToken);
        }

        var learnedAliases = _ocrAliasStore.Load();
        if (learnedAliases.Count > 0)
        {
            AppLog.Info(
                $"Кафе: загружено обученных OCR-псевдонимов: " +
                $"{learnedAliases.Count}.");
        }

        IReadOnlyList<FishCatalogItem> fishCatalog;
        try
        {
            fishCatalog = new FishCatalogStore().LoadCatalog();
        }
        catch (Exception exception)
        {
            AppLog.Warn($"Не удалось загрузить каталог рыб: {exception.Message}");
            fishCatalog = [];
        }

        var parsedSlotOffers = slots
            .Select(slot => ParseCard(slot.RawLines))
            .ToList();
        var dominantWaterBody = CafeWaterBodyInference.FindDominant(
            parsedSlotOffers.Select(offer => offer?.WaterBodyName));
        if (!string.IsNullOrWhiteSpace(dominantWaterBody))
        {
            AppLog.Info(
                $"Кафе: основной водоём снимка — {dominantWaterBody}.");
        }

        var offers = new List<CafeOffer>(slots.Count);
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var offer = parsedSlotOffers[index];
            var verified = verifiedFields[index];
            AppLog.OcrDetails(
                $"Кафе, слот {index + 1}: исходные строки=[" +
                $"{string.Join(" | ", slot.RawLines)}]; " +
                $"общее распознавание={(offer is null ? "нет" : "да")}; " +
                $"карточный OCR={(verified.HasAnyValue ? "да" : "нет")}.");
            if (!CafeCardSlotEvidence.ShouldInclude(
                    slot,
                    offer is not null,
                    verified.HasAnyValue))
            {
                AppLog.Info(
                    $"Кафе: слот сетки {index + 1} пуст — " +
                    "карточка отсутствует на снимке.");
                continue;
            }

            if (offer is null &&
                string.IsNullOrWhiteSpace(verified.FishName))
            {
                AppLog.Warn(
                    $"Кафе: карточка {index + 1} из {slots.Count} " +
                    "не распознана и оставлена для ручного ввода.");
                offers.Add(new CafeOffer
                {
                    RecognitionSource = "не распознано",
                    WeightSource =
                        $"карточка {index + 1}: не распознано"
                });
                continue;
            }

            var generalObservation = offer?.MinimumWeightGrams is { } general
                ? new CafeWeightObservation(
                    general,
                    offer.MinimumWeightUnit,
                    offer.RawWeightText,
                    string.IsNullOrWhiteSpace(offer.WeightSource)
                        ? "общий OCR"
                        : offer.WeightSource)
                : null;
            var decision = CafeWeightResolver.Resolve(
                generalObservation,
                verified.Weight);
            if (decision.HasConflict)
            {
                AppLog.Warn(
                    $"Конфликт порога кафе для «" +
                    $"{offer?.FishName ?? verified.FishName ?? "неизвестно"}»: " +
                    $"общий OCR={decision.Alternative!.Grams:0.##} г " +
                    $"(«{decision.Alternative.RawText}»); " +
                    $"OCR карточки={decision.Selected!.Grams:0.##} г " +
                    $"(«{decision.Selected.RawText}»). " +
                    "В окне проверки предложен результат отдельной карточки.");
            }

            var selectedWeight = decision.Selected;
            var alternativeWeight = decision.Alternative;
            var rawFishName = verified.FishName ?? offer?.FishName ?? "";
            var canonicalFish = CafeFishNameCanonicalizer.Canonicalize(
                rawFishName,
                fishCatalog,
                learnedAliases);
            var recognitionSource = offer is not null && verified.HasAnyValue
                ? "общий OCR + OCR карточки"
                : verified.HasAnyValue
                    ? "OCR карточки"
                    : "общий OCR";
            var storedOffer = new CafeOffer
            {
                FishName = canonicalFish.Name,
                RawFishName = canonicalFish.RawName,
                FishCatalogId = canonicalFish.CatalogId,
                RecognitionSource = recognitionSource,
                WaterBodyName = CafeWaterBodyInference.Resolve(
                    offer?.WaterBodyName,
                    dominantWaterBody),
                Quantity = offer is { Quantity: > 0 }
                    ? offer.Quantity
                    : verified.Quantity ?? 0,
                MinimumWeightGrams = selectedWeight?.Grams,
                MinimumWeightUnit = selectedWeight?.Unit ?? "",
                RawWeightText = selectedWeight?.RawText ?? "",
                WeightSource = selectedWeight?.Source ?? "",
                AlternativeMinimumWeightGrams =
                    alternativeWeight?.Grams,
                AlternativeMinimumWeightUnit =
                    alternativeWeight?.Unit ?? "",
                AlternativeRawWeightText =
                    alternativeWeight?.RawText ?? "",
                AlternativeWeightSource =
                    alternativeWeight?.Source ?? "",
                Price = offer?.Price
            };
            offers.Add(storedOffer);
            AppLog.Info(
                $"Кафе: {storedOffer.FishName}; " +
                $"водоём={storedOffer.WaterBodyName}; " +
                $"количество={storedOffer.Quantity}; " +
                $"минимальный вес=" +
                $"{storedOffer.MinimumWeightGrams?.ToString("0.##",
                    CultureInfo.InvariantCulture) ?? "не распознан"} г.");
        }

        var recognizedCount = offers.Count(offer =>
            !string.IsNullOrWhiteSpace(offer.FishName));
        AppLog.Info(
            $"Кафе: распознано карточек {recognizedCount} из " +
            $"{offers.Count}; строк проверки подготовлено={offers.Count}; " +
            $"пустых позиций сетки={slots.Count - offers.Count}.");
        return offers;
    }

    private static async Task<IReadOnlyList<RecognizedCardFields>>
        RecognizeCardFieldsAsync(
            string imagePath,
            uint imageWidth,
            uint imageHeight,
            OcrEngine engine,
            CancellationToken cancellationToken)
    {
        var fields = new List<RecognizedCardFields>(10);
        BitmapSource? source = null;
        try
        {
            source = LoadBitmapSource(imagePath);
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось подготовить изображение для OCR карточек: " +
                $"{exception.Message}");
        }

        if (source is null)
        {
            return Enumerable
                .Repeat(new RecognizedCardFields(null, null, null), 10)
                .ToList();
        }

        for (var index = 0; index < 10; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var column = index % 5;
            var row = index / 5;

            try
            {
                var xRatio = 0.226 + column * 0.149;
                var nameYRatio = row == 0 ? 0.285 : 0.675;
                var weightYRatio = row == 0 ? 0.420 : 0.810;
                var nameLines = await RecognizeRegionAsync(
                    source,
                    imageWidth,
                    imageHeight,
                    engine,
                    xRatio,
                    nameYRatio,
                    0.137,
                    0.085);
                // Увеличенная область веса начинается немного левее
                // стандартной области карточки. На дробных значениях вроде
                // «2,149 кг» Windows OCR иногда теряет первую цифру «2»,
                // если она оказывается на границе узкого crop.
                var weightLines = await RecognizeRegionAsync(
                    source,
                    imageWidth,
                    imageHeight,
                    engine,
                    // Оставляем запас слева для первой цифры дробного
                    // значения «2,149 кг». На узком crop OCR иногда
                    // возвращает только «149 кг».
                    Math.Max(0.0, xRatio - 0.045),
                    Math.Max(0.0, weightYRatio - 0.020),
                    0.260,
                    0.115);
                AppLog.OcrDetails(
                    $"OCR кафе, карточка {index + 1}: " +
                    $"название=[{string.Join(" | ", nameLines)}]; " +
                    $"вес=[{string.Join(" | ", weightLines)}]");
                var weight = ParseMinimumWeightDetails(weightLines);
                fields.Add(new RecognizedCardFields(
                    SelectCardName(nameLines),
                    null,
                    weight is null
                        ? null
                        : new CafeWeightObservation(
                            weight.Grams,
                            weight.Unit,
                            weight.RawText,
                            "OCR отдельной карточки")));
            }
            catch (Exception exception)
            {
                AppLog.Warn(
                    $"Не удалось отдельно проверить вес карточки кафе " +
                    $"{index + 1}: {exception.Message}");
                fields.Add(new RecognizedCardFields(null, null, null));
            }
        }

        return fields;
    }

    private async Task<IReadOnlyList<RecognizedCardFields>>
        RecognizePaddleCardFieldsAsync(
            string imagePath,
            uint imageWidth,
            uint imageHeight,
            CancellationToken cancellationToken)
    {
        var fields = new List<RecognizedCardFields>(10);
        try
        {
            using var source = new Bitmap(imagePath);
            for (var index = 0; index < 10; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cropPath = Path.Combine(
                    Path.GetTempPath(),
                    $"RF4AssistantPro_paddle_cafe_{Guid.NewGuid():N}.png");
                try
                {
                    using var crop = CreatePaddleCardCrop(
                        source,
                        imageWidth,
                        imageHeight,
                        index);
                    crop.Save(cropPath, ImageFormat.Png);
                    var paddleLines = await _paddleOcr.RecognizeAsync(
                        cropPath,
                        cancellationToken);
                    var lines = CafeOcrTextLayout.ComposeLines(
                            paddleLines.Select(line => new CafeOcrBlock(
                                line.Text,
                                line.Left,
                                line.Top,
                                line.Right,
                                line.Bottom)))
                        .Select(line => Normalize(line.Text))
                        .Where(line => line.Length > 0)
                        .ToList();
                    var parsed = ParseCard(lines);
                    var fishName = SelectCardName(lines) ??
                        parsed?.FishName;
                    IReadOnlyList<string> focusedNameLines = [];
                    if (string.IsNullOrWhiteSpace(fishName))
                    {
                        focusedNameLines = await RecognizePaddleNameAsync(
                            source,
                            imageWidth,
                            imageHeight,
                            index,
                            cancellationToken);
                        fishName = SelectCardName(focusedNameLines);
                    }
                    fields.Add(new RecognizedCardFields(
                        fishName,
                        parsed is { Quantity: > 0 }
                            ? parsed.Quantity
                            : null,
                        parsed?.MinimumWeightGrams is { } grams
                            ? new CafeWeightObservation(
                                grams,
                                parsed.MinimumWeightUnit,
                                parsed.RawWeightText,
                                "OCR карточки Paddle")
                            : null));
                    AppLog.OcrDetails(
                        $"PaddleOCR кафе, карточка {index + 1}: " +
                        $"{string.Join(" | ", lines)}; " +
                        $"фокус названия=[{string.Join(" | ", focusedNameLines)}]");
                    if (string.IsNullOrWhiteSpace(fishName))
                    {
                        AppLog.Warn(
                            $"PaddleOCR не распознал название карточки " +
                            $"{index + 1}. Общие строки=[" +
                            $"{string.Join(" | ", lines)}]; " +
                            $"фокус=[{string.Join(" | ", focusedNameLines)}].");
                    }
                }
                catch (Exception exception)
                {
                    AppLog.Warn(
                        $"PaddleOCR не смог проверить карточку кафе " +
                        $"{index + 1}: {exception.Message}");
                    fields.Add(new RecognizedCardFields(null, null, null));
                }
                finally
                {
                    try
                    {
                        File.Delete(cropPath);
                    }
                    catch
                    {
                        // Временный файл удалит системная очистка.
                    }
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось подготовить crop-карточки для PaddleOCR: " +
                $"{exception.Message}");
        }

        while (fields.Count < 10)
        {
            fields.Add(new RecognizedCardFields(null, null, null));
        }

        return fields;
    }

    private async Task<IReadOnlyList<string>> RecognizePaddleNameAsync(
        Bitmap source,
        uint imageWidth,
        uint imageHeight,
        int index,
        CancellationToken cancellationToken)
    {
        var cropPath = Path.Combine(
            Path.GetTempPath(),
            $"RF4AssistantPro_paddle_cafe_name_{Guid.NewGuid():N}.png");
        var contrastPath = Path.Combine(
            Path.GetTempPath(),
            $"RF4AssistantPro_paddle_cafe_name_bw_{Guid.NewGuid():N}.png");
        try
        {
            using var crop = CreatePaddleNameCrop(
                source,
                imageWidth,
                imageHeight,
                index);
            crop.Save(cropPath, ImageFormat.Png);
            var regular = await RecognizePaddleLinesAsync(
                cropPath,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(SelectCardName(regular)))
            {
                return regular;
            }

            using var highContrast = CreateHighContrastNameCrop(crop);
            highContrast.Save(contrastPath, ImageFormat.Png);
            var contrasted = await RecognizePaddleLinesAsync(
                contrastPath,
                cancellationToken);
            return !string.IsNullOrWhiteSpace(SelectCardName(contrasted))
                ? contrasted
                : regular.Concat(contrasted).Distinct().ToList();
        }
        finally
        {
            foreach (var path in new[] { cropPath, contrastPath })
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // Временный файл удалит системная очистка.
                }
            }
        }
    }

    private async Task<IReadOnlyList<string>> RecognizePaddleLinesAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        var blocks = await _paddleOcr.RecognizeAsync(
            imagePath,
            cancellationToken);
        return CafeOcrTextLayout.ComposeLines(
                blocks.Select(line => new CafeOcrBlock(
                    line.Text,
                    line.Left,
                    line.Top,
                    line.Right,
                    line.Bottom)))
            .Select(line => Normalize(line.Text))
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static Bitmap CreateHighContrastNameCrop(Bitmap source)
    {
        var result = new Bitmap(
            source.Width,
            source.Height,
            DrawingPixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(result);
        using var attributes = new ImageAttributes();
        var grayscale = new ColorMatrix(
        [
            [0.299f, 0.299f, 0.299f, 0, 0],
            [0.587f, 0.587f, 0.587f, 0, 0],
            [0.114f, 0.114f, 0.114f, 0, 0],
            [0, 0, 0, 1, 0],
            [0, 0, 0, 0, 1]
        ]);
        attributes.SetColorMatrix(grayscale);
        attributes.SetThreshold(0.62f);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, result.Width, result.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
        return result;
    }

    private static Bitmap CreatePaddleNameCrop(
        Bitmap source,
        uint imageWidth,
        uint imageHeight,
        int index)
    {
        var column = index % 5;
        var row = index / 5;
        var x = (int)Math.Round(imageWidth * (0.215 + column * 0.149));
        var y = (int)Math.Round(imageHeight * (row == 0 ? 0.288 : 0.684));
        var width = (int)Math.Round(imageWidth * 0.145);
        var height = (int)Math.Round(imageHeight * 0.052);

        x = Math.Clamp(x, 0, Math.Max(0, source.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, source.Height - 1));
        width = Math.Clamp(width, 1, source.Width - x);
        height = Math.Clamp(height, 1, source.Height - y);

        const double scale = 4.0;
        var crop = new Bitmap(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)),
            DrawingPixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(crop);
        graphics.Clear(System.Drawing.Color.Black);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, crop.Width, crop.Height),
            new Rectangle(x, y, width, height),
            GraphicsUnit.Pixel);
        return crop;
    }

    private static Bitmap CreatePaddleCardCrop(
        Bitmap source,
        uint imageWidth,
        uint imageHeight,
        int index)
    {
        var column = index % 5;
        var row = index / 5;
        var x = (int)Math.Round(
            imageWidth * (0.205 + column * 0.149));
        var y = (int)Math.Round(
            imageHeight * (row == 0 ? 0.245 : 0.635));
        var width = (int)Math.Round(imageWidth * 0.18);
        var height = (int)Math.Round(imageHeight * 0.29);

        x = Math.Clamp(x, 0, Math.Max(0, source.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, source.Height - 1));
        width = Math.Clamp(width, 1, source.Width - x);
        height = Math.Clamp(height, 1, source.Height - y);

        const double scale = 3.0;
        var crop = new Bitmap(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)),
            DrawingPixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(crop);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, crop.Width, crop.Height),
            new Rectangle(x, y, width, height),
            GraphicsUnit.Pixel);
        return crop;
    }

    private static IReadOnlyList<RecognizedCardFields> MergeCardFields(
        IReadOnlyList<RecognizedCardFields> primary,
        IReadOnlyList<RecognizedCardFields>? fallback)
    {
        var result = new List<RecognizedCardFields>(10);
        for (var index = 0; index < 10; index++)
        {
            var selected = primary[index];
            var backup = fallback is not null && index < fallback.Count
                ? fallback[index]
                : new RecognizedCardFields(null, null, null);
            result.Add(new RecognizedCardFields(
                selected.FishName ?? backup.FishName,
                selected.Quantity ?? backup.Quantity,
                selected.Weight ?? backup.Weight));
        }

        return result;
    }

    private static async Task<IReadOnlyList<string>> RecognizeRegionAsync(
        BitmapSource source,
        uint imageWidth,
        uint imageHeight,
        OcrEngine engine,
        double xRatio,
        double yRatio,
        double widthRatio,
        double heightRatio)
    {
        var x = Math.Min(
            (uint)(imageWidth * xRatio),
            imageWidth - 1);
        var y = Math.Min(
            (uint)(imageHeight * yRatio),
            imageHeight - 1);
        var width = Math.Min(
            Math.Max(1u, (uint)(imageWidth * widthRatio)),
            imageWidth - x);
        var height = Math.Min(
            Math.Max(1u, (uint)(imageHeight * heightRatio)),
            imageHeight - y);

        var crop = new CroppedBitmap(
            source,
            new Int32Rect(
                checked((int)x),
                checked((int)y),
                checked((int)width),
                checked((int)height)));
        crop.Freeze();
        var enlarged = new TransformedBitmap(
            crop,
            new ScaleTransform(3.0, 3.0));
        enlarged.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(WpfBitmapFrame.Create(enlarged));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        var png = memory.ToArray();

        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(
            randomAccessStream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        randomAccessStream.Seek(0);
        var regionDecoder =
            await WinBitmapDecoder.CreateAsync(randomAccessStream);
        using var bitmap = await regionDecoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);
        return result.Lines
            .Select(line => Normalize(line.Text))
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static BitmapSource LoadBitmapSource(string imagePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static IReadOnlyList<CafeCardSlot> ParseCardSlots(
        IReadOnlyList<OcrLine> lines,
        uint imageWidth,
        uint imageHeight)
    {
        return CafeCardSlotLayout.Build(
            lines.Select(line => new CafePositionedLine(
                line.Text,
                line.CenterX,
                line.CenterY)),
            imageWidth,
            imageHeight);
    }

    private static CafeOffer? ParseCard(IReadOnlyList<string> sourceLines)
    {
        var lines = sourceLines
            .Where(line => line.Length > 0)
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        var fishName = lines.FirstOrDefault(IsName) ?? "";
        if (fishName.Equals("Водоём", StringComparison.OrdinalIgnoreCase))
        {
            fishName = "";
        }
        var waterBody = lines.FirstOrDefault(IsWaterBody) ?? "";
        var quantity = CafeQuantityTextParser.TryParse(lines) ?? 0;

        var minimumWeight = ParseMinimumWeightDetails(lines);

        decimal? price = null;
        var priceLine = lines.LastOrDefault(line => PriceRegex.IsMatch(line));
        if (priceLine is not null &&
            decimal.TryParse(
                priceLine.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedPrice))
        {
            price = parsedPrice;
        }

        // Сохраняем частично распознанный слот, если OCR нашёл хотя бы
        // количество, вес, водоём или цену. Название сможет восстановить
        // дополнительный crop- проход Windows OCR.
        if (fishName.Length == 0 &&
            quantity == 0 &&
            minimumWeight is null &&
            string.IsNullOrWhiteSpace(waterBody) &&
            price is null)
        {
            return null;
        }

        return new CafeOffer
        {
            FishName = fishName,
            WaterBodyName = waterBody,
            Quantity = quantity,
            MinimumWeightGrams = minimumWeight?.Grams,
            MinimumWeightUnit = minimumWeight?.Unit ?? "",
            RawWeightText = minimumWeight?.RawText ?? "",
            WeightSource = minimumWeight is null
                ? ""
                : "общий OCR",
            Price = price
        };
    }

    private static decimal? ParseMinimumWeightGrams(
        IEnumerable<string> lines)
    {
        return ParseMinimumWeightDetails(lines)?.Grams;
    }

    private static CafeParsedWeight? ParseMinimumWeightDetails(
        IEnumerable<string> lines)
    {
        return CafeWeightTextParser.TryParse(lines);
    }

    private static bool IsName(string line)
    {
        return NameRegex.IsMatch(line) &&
               !WeightRegex.IsMatch(line) &&
               !line.Equals("Кафе", StringComparison.OrdinalIgnoreCase) &&
               !line.Equals("Заказы", StringComparison.OrdinalIgnoreCase) &&
               !line.Equals("Количество", StringComparison.OrdinalIgnoreCase) &&
               !line.Equals("Масса", StringComparison.OrdinalIgnoreCase) &&
               !line.Equals("Цена", StringComparison.OrdinalIgnoreCase) &&
               !line.Equals("шт", StringComparison.OrdinalIgnoreCase) &&
               !line.Contains("Водоём", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SelectCardName(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var directName = lines[index];
            if (!IsName(directName))
            {
                continue;
            }

            if (index >= 0 &&
                index + 1 < lines.Count &&
                Regex.IsMatch(
                    lines[index + 1],
                    @"^[а-яё][а-яё\-\s]{2,}$",
                    RegexOptions.CultureInvariant))
            {
                return $"{directName} {lines[index + 1]}";
            }

            return directName;
        }

        return null;
    }

    private static bool IsWaterBody(string line)
    {
        return line.StartsWith("р.", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("оз.", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("море", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("архипелаг", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string line)
    {
        return string.Join(
            " ",
            line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record OcrLine(string Text, double CenterX, double CenterY);

    private sealed record RecognizedCardFields(
        string? FishName,
        int? Quantity,
        CafeWeightObservation? Weight)
    {
        public bool HasAnyValue =>
            !string.IsNullOrWhiteSpace(FishName) ||
            Quantity.HasValue ||
            Weight is not null;
    }

    private static OcrEngine? TryCreateRussianEngine()
    {
        try
        {
            return OcrEngine.TryCreateFromLanguage(new Language("ru-RU"))
                ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }
    }
}