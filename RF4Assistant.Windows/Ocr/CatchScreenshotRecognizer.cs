using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using RF4AssistantPro.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Ocr;

public sealed class CatchScreenshotRecognizer
{
    private static readonly Regex WeightCandidateRegex = new(
        @"(?<![\d\-–—])(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>кг|г|r)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BareNumberRegex = new(
        @"(?<!\d)(?<value>\d{1,5}(?:[.,]\d+)?)(?!\d)",
        RegexOptions.CultureInvariant);

    public async Task<RecognizedCatch?> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        var engine = TryCreateRussianEngine();
        if (engine is null)
        {
            throw new InvalidOperationException(
                "В Windows не установлен пакет распознавания русского языка.");
        }

        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();

        // Название рыбы находится в верхней центральной части экрана.
        // На полном скриншоте OCR иногда пропускает его из-за большого
        // количества деталей и принимает слово «БОНУС» за название рыбы.
        // Поэтому отдельно распознаём увеличенную область с заголовком.
        var titleLines = await RecognizeTitleAsync(decoder, engine);
        cancellationToken.ThrowIfCancellationRequested();
        var metricLines = await RecognizeMetricsAsync(decoder, engine);
        cancellationToken.ThrowIfCancellationRequested();
        var enhancedFields = await RecognizeEnhancedTopFieldsAsync(
            imagePath,
            engine);
        titleLines = titleLines
            .Concat(enhancedFields.TitleLines)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        metricLines = metricLines
            .Concat(enhancedFields.MetricLines)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();

        var sourceLines = result.Lines
            .Select(line => line.Text)
            .Concat(metricLines)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppLog.OcrDetails(
            "OCR улова, полный текст: " +
            string.Join(" | ", sourceLines));
        AppLog.OcrDetails(
            "OCR улова, поля веса и длины: " +
            string.Join(" | ", metricLines));
        var recognized = CatchTextParser.Parse(sourceLines, titleLines);
        var usedTitleFallback = false;
        if (recognized is null)
        {
            // Иногда увеличенная область заголовка содержит и название,
            // и вес, хотя полный кадр и отдельная область веса их пропустили.
            // Используем её только как резервный источник, чтобы конфликтующее
            // значение заголовка не заменяло успешный основной результат.
            var fallbackLines = sourceLines
                .Concat(titleLines)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            recognized = CatchTextParser.Parse(fallbackLines, titleLines);
            if (recognized is not null)
            {
                usedTitleFallback = true;
                AppLog.Warn(
                    "Улов восстановлен резервным OCR заголовка: " +
                    $"{recognized.FishName}; " +
                    $"{FormatWeight(recognized.WeightKg)}.");
            }
        }

        if (recognized is null)
        {
            AppLog.Info(
                "OCR улова отклонён: не удалось надёжно определить " +
                "название или вес на карточке рыбы.");
            AppLog.OcrDetails(
                "OCR улова, диагностический текст: полный=[" +
                string.Join(" | ", sourceLines) +
                "]; заголовок=[" +
                string.Join(" | ", titleLines) + "].");
        }
        else
        {
            LogWeightSelection(
                recognized.WeightKg,
                result.Lines.Select(line => line.Text),
                metricLines,
                titleLines);
        }

        if (recognized is not null &&
            (usedTitleFallback || enhancedFields.UsedBareWeight))
        {
            var reasons = new List<string>();
            if (usedTitleFallback)
            {
                reasons.Add("резервный OCR заголовка");
            }

            if (enhancedFields.UsedBareWeight)
            {
                reasons.Add("единица веса восстановлена по формату числа");
            }

            recognized = CopyForReview(
                recognized,
                string.Join("; ", reasons));
            AppLog.Warn(
                $"Улов требует проверки: {recognized.FishName}; " +
                $"{FormatWeight(recognized.WeightKg)}; " +
                $"{recognized.ReviewReason}.");
        }

        return recognized;
    }

    private static RecognizedCatch CopyForReview(
        RecognizedCatch source,
        string reason)
    {
        return new RecognizedCatch
        {
            FishName = source.FishName,
            WaterBodyName = source.WaterBodyName,
            WeightKg = source.WeightKg,
            LengthCm = source.LengthCm,
            Quality = source.Quality,
            RawText = source.RawText,
            NeedsReview = true,
            ReviewReason = reason
        };
    }

    private static void LogWeightSelection(
        decimal selectedWeightKg,
        IEnumerable<string> fullFrameLines,
        IEnumerable<string> metricLines,
        IEnumerable<string> titleLines)
    {
        var candidates = ReadWeightCandidates(fullFrameLines, "полный кадр")
            .Concat(ReadWeightCandidates(metricLines, "область веса/длины"))
            .Concat(ReadWeightCandidates(titleLines, "заголовок"))
            .Distinct()
            .ToList();
        var distinctWeights = candidates
            .Select(candidate => candidate.WeightKg)
            .Distinct()
            .ToList();

        if (distinctWeights.Count <= 1)
        {
            return;
        }

        var selectedSources = candidates
            .Where(candidate => candidate.WeightKg == selectedWeightKg)
            .Select(candidate => candidate.Source)
            .Distinct()
            .ToList();
        var selectedSource = selectedSources.Count == 0
            ? "основной разбор карточки"
            : string.Join(", ", selectedSources);
        var alternatives = candidates
            .Where(candidate => candidate.WeightKg != selectedWeightKg)
            .Select(candidate =>
                $"{FormatWeight(candidate.WeightKg)} ({candidate.Source}: " +
                $"«{candidate.Text}»)")
            .Distinct()
            .ToList();

        AppLog.Warn(
            $"Конфликт веса OCR: выбран {FormatWeight(selectedWeightKg)} " +
            $"из источника «{selectedSource}»; альтернативы: " +
            $"{string.Join("; ", alternatives)}.");
    }

    private static IEnumerable<WeightCandidate> ReadWeightCandidates(
        IEnumerable<string> lines,
        string source)
    {
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            foreach (Match match in WeightCandidateRegex.Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["value"].Value.Replace(',', '.'),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    continue;
                }

                var weightKg = match.Groups["unit"].Value.Equals(
                    "кг",
                    StringComparison.OrdinalIgnoreCase)
                    ? value
                    : value / 1000m;
                yield return new WeightCandidate(weightKg, source, line);
            }
        }
    }

    private static string FormatWeight(decimal weightKg)
    {
        return weightKg < 1m
            ? $"{weightKg * 1000m:0.##} г"
            : $"{weightKg:0.###} кг";
    }

    private sealed record WeightCandidate(
        decimal WeightKg,
        string Source,
        string Text);

    private sealed record EnhancedCatchFields(
        IReadOnlyList<string> TitleLines,
        IReadOnlyList<string> MetricLines,
        bool UsedBareWeight);

    private static async Task<EnhancedCatchFields>
        RecognizeEnhancedTopFieldsAsync(
            string imagePath,
            OcrEngine engine)
    {
        var titleLines = new List<string>();
        var metricLines = new List<string>();
        var temporaryFiles = new List<string>();
        var usedBareWeight = false;

        try
        {
            using var source = new Bitmap(imagePath);
            var calibratedLayout = TryCalibrateLayout(source);
            var qualityRegion = calibratedLayout?.QualityBadge;
            if (HasGreenQualityBadge(source, qualityRegion))
            {
                metricLines.Add("Зачётная");
            }

            var regions = calibratedLayout is null
                ? new[]
                {
                    (X: 0.43, Y: 0.055, Width: 0.14, Height: 0.060,
                        IsTitle: true, AllowBareWeight: false),
                    (X: 0.395, Y: 0.115, Width: 0.075, Height: 0.055,
                        IsTitle: false, AllowBareWeight: true),
                    (X: 0.438, Y: 0.115, Width: 0.068, Height: 0.055,
                        IsTitle: false, AllowBareWeight: true),
                    (X: 0.515, Y: 0.112, Width: 0.085, Height: 0.065,
                        IsTitle: false, AllowBareWeight: false)
                }
                : new[]
                {
                    ToRatioRegion(
                        calibratedLayout.Title,
                        source,
                        true,
                        false),
                    ToRatioRegion(
                        calibratedLayout.ThreeBadgeWeight,
                        source,
                        false,
                        true),
                    ToRatioRegion(
                        calibratedLayout.TwoBadgeWeight,
                        source,
                        false,
                        true),
                    ToRatioRegion(
                        calibratedLayout.QualityBadge,
                        source,
                        false,
                        false)
                };

            if (calibratedLayout is not null)
            {
                AppLog.OcrDetails(
                    "Автокалибровка карточки улова: шкала " +
                    $"x={calibratedLayout.RulerLeft}.." +
                    $"{calibratedLayout.RulerRight}, " +
                    $"y={calibratedLayout.RulerY}, " +
                    $"масштаб={calibratedLayout.Scale:0.###}.");
            }
            else
            {
                AppLog.OcrDetails(
                    "Автокалибровка карточки улова: шкала не найдена, " +
                    "используются резервные относительные области.");
            }

            foreach (var region in regions)
            {
                foreach (var threshold in new byte[] { 145, 180 })
                {
                    var path = CreateThresholdCrop(
                        source,
                        region.X,
                        region.Y,
                        region.Width,
                        region.Height,
                        6.0,
                        threshold);
                    temporaryFiles.Add(path);
                    var recognizedLines = await ReadOcrLinesAsync(
                        path,
                        engine);

                    if (region.IsTitle)
                    {
                        titleLines.AddRange(recognizedLines);
                        continue;
                    }

                    metricLines.AddRange(recognizedLines);
                    if (region.AllowBareWeight &&
                        !recognizedLines.Any(line =>
                            WeightCandidateRegex.IsMatch(line)))
                    {
                        foreach (var line in recognizedLines)
                        {
                            var match = BareNumberRegex.Match(line);
                            if (match.Success)
                            {
                                usedBareWeight = true;
                                metricLines.Add(FormatBareWeight(
                                    match.Groups["value"].Value));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                "Резервная обработка верхней панели улова недоступна: " +
                exception.Message);
        }
        finally
        {
            foreach (var path in temporaryFiles)
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

        return new EnhancedCatchFields(
            titleLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            metricLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            usedBareWeight);
    }

    private static string CreateThresholdCrop(
        Bitmap source,
        double xRatio,
        double yRatio,
        double widthRatio,
        double heightRatio,
        double scale,
        byte threshold)
    {
        var x = Math.Clamp(
            (int)Math.Round(source.Width * xRatio),
            0,
            source.Width - 1);
        var y = Math.Clamp(
            (int)Math.Round(source.Height * yRatio),
            0,
            source.Height - 1);
        var width = Math.Max(
            1,
            Math.Min(
                source.Width - x,
                (int)Math.Round(source.Width * widthRatio)));
        var height = Math.Max(
            1,
            Math.Min(
                source.Height - y,
                (int)Math.Round(source.Height * heightRatio)));
        using var crop = new Bitmap(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)),
            PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(crop))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, crop.Width, crop.Height),
                new Rectangle(x, y, width, height),
                GraphicsUnit.Pixel);
        }

        var data = crop.LockBits(
            new Rectangle(0, 0, crop.Width, crop.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                for (var row = 0; row < data.Height; row++)
                {
                    var pixels = (byte*)data.Scan0 + row * data.Stride;
                    for (var column = 0; column < data.Width; column++)
                    {
                        var offset = column * 3;
                        var luminance =
                            (pixels[offset] * 29 +
                             pixels[offset + 1] * 150 +
                             pixels[offset + 2] * 77) >> 8;
                        var value = luminance >= threshold
                            ? (byte)0
                            : (byte)255;
                        pixels[offset] = value;
                        pixels[offset + 1] = value;
                        pixels[offset + 2] = value;
                    }
                }
            }
        }
        finally
        {
            crop.UnlockBits(data);
        }

        var path = Path.Combine(
            Path.GetTempPath(),
            $"RF4AssistantPro_catch_field_{Guid.NewGuid():N}.png");
        crop.Save(path, ImageFormat.Png);
        return path;
    }

    private static string FormatBareWeight(string value)
    {
        // RF4 показывает граммы целым числом, а килограммы — дробным
        // значением с запятой или точкой. Название рыбы здесь не учитывается:
        // одна и та же рыба может отображаться в обеих единицах.
        var unit = value.Contains(',') || value.Contains('.')
            ? "кг"
            : "г";
        return $"{value} {unit}";
    }

    private static (
        double X,
        double Y,
        double Width,
        double Height,
        bool IsTitle,
        bool AllowBareWeight) ToRatioRegion(
            CatchPixelRegion region,
            Bitmap source,
            bool isTitle,
            bool allowBareWeight)
    {
        return (
            region.X / (double)source.Width,
            region.Y / (double)source.Height,
            region.Width / (double)source.Width,
            region.Height / (double)source.Height,
            isTitle,
            allowBareWeight);
    }

    private static CatchCalibratedLayout? TryCalibrateLayout(Bitmap source)
    {
        var luminance = new byte[checked(source.Width * source.Height)];
        var scanHeight = Math.Min(
            source.Height,
            (int)Math.Ceiling(source.Height * 0.36));
        using var upper = new Bitmap(
            source.Width,
            scanHeight,
            PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(upper))
        {
            graphics.DrawImageUnscaled(source, 0, 0);
        }

        var data = upper.LockBits(
            new Rectangle(0, 0, upper.Width, upper.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                for (var y = 0; y < data.Height; y++)
                {
                    var pixels = (byte*)data.Scan0 + y * data.Stride;
                    var output = y * source.Width;
                    for (var x = 0; x < data.Width; x++)
                    {
                        var offset = x * 3;
                        luminance[output + x] = (byte)(
                            (pixels[offset] * 29 +
                             pixels[offset + 1] * 150 +
                             pixels[offset + 2] * 77) >> 8);
                    }
                }
            }
        }
        finally
        {
            upper.UnlockBits(data);
        }

        return CatchLayoutCalibrator.TryCalibrate(
            luminance,
            source.Width,
            source.Height);
    }

    private static bool HasGreenQualityBadge(
        Bitmap source,
        CatchPixelRegion? calibratedRegion)
    {
        var x = calibratedRegion?.X ??
                Math.Clamp(
                    (int)Math.Round(source.Width * 0.515),
                    0,
                    source.Width - 1);
        var y = calibratedRegion?.Y ??
                Math.Clamp(
                    (int)Math.Round(source.Height * 0.112),
                    0,
                    source.Height - 1);
        var width = calibratedRegion?.Width ??
                    Math.Max(
                        1,
                        Math.Min(
                            source.Width - x,
                            (int)Math.Round(source.Width * 0.085)));
        var height = calibratedRegion?.Height ??
                     Math.Max(
                         1,
                         Math.Min(
                             source.Height - y,
                             (int)Math.Round(source.Height * 0.065)));
        var greenPixels = 0;
        var sampledPixels = 0;

        for (var row = y; row < y + height; row += 2)
        {
            for (var column = x; column < x + width; column += 2)
            {
                var color = source.GetPixel(column, row);
                sampledPixels++;
                if (color.G >= 130 &&
                    color.G > color.R * 1.25 &&
                    color.G > color.B * 1.80)
                {
                    greenPixels++;
                }
            }
        }

        return sampledPixels > 0 &&
               greenPixels >= sampledPixels * 0.18;
    }

    private static async Task<IReadOnlyList<string>> ReadOcrLinesAsync(
        string imagePath,
        OcrEngine engine)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);
        return result.Lines
            .Select(line => line.Text)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> RecognizeMetricsAsync(
        BitmapDecoder decoder,
        OcrEngine engine)
    {
        var sourceWidth = decoder.PixelWidth;
        var sourceHeight = decoder.PixelHeight;
        var lines = new List<string>();

        // На карточке вес и длина находятся непосредственно под названием.
        // Полный OCR иногда сливает «57 г 15 см» в «57 г исм», поэтому
        // распознаём каждое поле отдельно и ещё раз — общую полосу.
        var regions = new[]
        {
            (X: 0.395, Y: 0.105, Width: 0.085, Height: 0.085, Scale: 4.0,
                AllowBareWeight: false),
            (X: 0.455, Y: 0.105, Width: 0.095, Height: 0.085, Scale: 4.0,
                AllowBareWeight: false),
            (X: 0.385, Y: 0.100, Width: 0.245, Height: 0.095, Scale: 3.2,
                AllowBareWeight: false),
            // Общая верхняя панель нового интерфейса: название, вес и длина.
            (X: 0.36, Y: 0.055, Width: 0.28, Height: 0.125, Scale: 4.0,
                AllowBareWeight: false),
            // Когда у рыбы нет плашки качества, две плашки центрируются и
            // вес смещается вправо. Windows OCR иногда читает только число,
            // пропуская маленькую «г». Для узких областей веса единица
            // известна из расположения, поэтому безопасно восстанавливаем её.
            (X: 0.400, Y: 0.116, Width: 0.070, Height: 0.052, Scale: 5.0,
                AllowBareWeight: true),
            (X: 0.438, Y: 0.116, Width: 0.066, Height: 0.052, Scale: 5.0,
                AllowBareWeight: true)
        };

        foreach (var region in regions)
        {
            try
            {
                var x = (uint)(sourceWidth * region.X);
                var y = (uint)(sourceHeight * region.Y);
                var width = Math.Max(
                    1u,
                    (uint)(sourceWidth * region.Width));
                var height = Math.Max(
                    1u,
                    (uint)(sourceHeight * region.Height));
                var boundedWidth = Math.Min(width, sourceWidth - x);
                var boundedHeight = Math.Min(height, sourceHeight - y);

                var transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = x,
                        Y = y,
                        Width = boundedWidth,
                        Height = boundedHeight
                    },
                    ScaledWidth = Math.Min(
                        (uint)(boundedWidth * region.Scale),
                        OcrEngine.MaxImageDimension),
                    ScaledHeight = Math.Min(
                        (uint)(boundedHeight * region.Scale),
                        OcrEngine.MaxImageDimension)
                };

                using var metricBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var metricResult = await engine.RecognizeAsync(metricBitmap);
                var recognizedLines = metricResult.Lines
                    .Select(line => line.Text)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                lines.AddRange(recognizedLines);

                if (region.AllowBareWeight &&
                    !recognizedLines.Any(line =>
                        WeightCandidateRegex.IsMatch(line)))
                {
                    foreach (var line in recognizedLines)
                    {
                        var match = BareNumberRegex.Match(line);
                        if (match.Success)
                        {
                            lines.Add(FormatBareWeight(
                                match.Groups["value"].Value));
                        }
                    }
                }
            }
            catch
            {
                // Полный кадр и остальные области всё ещё доступны.
            }
        }

        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> RecognizeTitleAsync(
        BitmapDecoder decoder,
        OcrEngine engine)
    {
        var sourceWidth = decoder.PixelWidth;
        var sourceHeight = decoder.PixelHeight;
        var lines = new List<string>();

        // Игра размещает название в верхней центральной области, но позиция
        // немного меняется при разных разрешениях и масштабе интерфейса.
        // Читаем несколько перекрывающихся областей и затем выбираем лучший
        // результат, а не первую короткую строку OCR.
        var regions = new[]
        {
            // Узкая область самого названия уменьшает влияние фона и рыбы.
            (X: 0.42, Y: 0.055, Width: 0.16, Height: 0.052, Scale: 5.0),
            // Точная область названия на карточке улова.
            (X: 0.35, Y: 0.03, Width: 0.30, Height: 0.10, Scale: 3.2),
            // Новый интерфейс 1920×1080: захватываем заголовок вместе с
            // расположенными ниже весом и длиной для резервного разбора.
            (X: 0.36, Y: 0.055, Width: 0.28, Height: 0.12, Scale: 4.0),
            (X: 0.20, Y: 0.02, Width: 0.60, Height: 0.12, Scale: 2.0),
            (X: 0.10, Y: 0.00, Width: 0.80, Height: 0.18, Scale: 2.2),
            (X: 0.25, Y: 0.00, Width: 0.50, Height: 0.20, Scale: 2.6)
        };

        foreach (var region in regions)
        {
            try
            {
                var x = (uint)(sourceWidth * region.X);
                var y = (uint)(sourceHeight * region.Y);
                var width = Math.Max(
                    1u,
                    (uint)(sourceWidth * region.Width));
                var height = Math.Max(
                    1u,
                    (uint)(sourceHeight * region.Height));
                var boundedWidth = Math.Min(width, sourceWidth - x);
                var boundedHeight = Math.Min(height, sourceHeight - y);

                var transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = x,
                        Y = y,
                        Width = boundedWidth,
                        Height = boundedHeight
                    },
                    ScaledWidth = Math.Min(
                        (uint)(boundedWidth * region.Scale),
                        OcrEngine.MaxImageDimension),
                    ScaledHeight = Math.Min(
                        (uint)(boundedHeight * region.Scale),
                        OcrEngine.MaxImageDimension)
                };

                using var titleBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var titleResult = await engine.RecognizeAsync(titleBitmap);
                lines.AddRange(
                    titleResult.Lines.Select(line => line.Text));
            }
            catch
            {
                // Остальные области всё ещё могут дать корректный результат.
            }
        }

        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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