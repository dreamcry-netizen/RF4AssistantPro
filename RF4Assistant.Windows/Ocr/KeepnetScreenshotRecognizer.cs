using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Ocr;

public sealed class KeepnetScreenshotRecognizer
{
    private const int MaximumCards = 150;

    private static readonly Regex WeightTokenRegex = new(
        @"^(?<value>\d+(?:[.,]\d+)?)(?<unit>кг|г)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UnitRegex = new(
        @"^(кг|г)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Одиночная «г» является единицей веса, а не частью названия рыбы.
    private static readonly Regex FishWordRegex = new(
        @"^[А-ЯЁа-яё\-]{2,}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex CapacityRegex = new(
        @"(?<!\d)(?<count>\d{1,3})\s*[/|\\]?\s*150(?!\d)",
        RegexOptions.CultureInvariant);

    public async Task<int?> DetectFishCountAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var engine = TryCreateRussianEngine() ??
            throw new InvalidOperationException(
                "В Windows не установлен пакет распознавания русского языка.");
        string? cropPath = null;

        try
        {
            using var source = new Bitmap(imagePath);
            var x = (int)Math.Round(source.Width * 0.045);
            var y = (int)Math.Round(source.Height * 0.175);
            var width = Math.Min(
                source.Width - x,
                (int)Math.Round(source.Width * 0.155));
            var height = Math.Min(
                source.Height - y,
                (int)Math.Round(source.Height * 0.105));
            const double scale = 4.0;
            using var crop = new Bitmap(
                Math.Max(1, (int)Math.Round(width * scale)),
                Math.Max(1, (int)Math.Round(height * scale)),
                PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(crop))
            {
                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, crop.Width, crop.Height),
                    new Rectangle(x, y, width, height),
                    GraphicsUnit.Pixel);
            }

            cropPath = Path.Combine(
                Path.GetTempPath(),
                $"RF4AssistantPro_capacity_{Guid.NewGuid():N}.png");
            crop.Save(cropPath, ImageFormat.Png);
            var lines = await ReadOcrLinesAsync(cropPath, engine);
            cancellationToken.ThrowIfCancellationRequested();
            var text = string.Join(" ", lines);
            var match = CapacityRegex.Match(text);
            if (!match.Success ||
                !int.TryParse(
                    match.Groups["count"].Value,
                    out var count) ||
                count is < 0 or > MaximumCards)
            {
                AppLog.Info(
                    $"Счётчик садка не распознан. OCR: {text}");
                return null;
            }

            AppLog.Info($"Счётчик садка распознан: {count}/150.");
            return count;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(cropPath))
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

    public async Task<IReadOnlyList<RecognizedKeepnetFish>> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var engine = TryCreateRussianEngine() ??
            throw new InvalidOperationException(
                "В Windows не установлен пакет распознавания русского языка.");
        var passes =
            new List<IReadOnlyList<RecognizedKeepnetFish>>();
        string? enlargedGridPath = null;

        try
        {
            // Первый проход сохраняет совместимость с кадрами нестандартного
            // разрешения. Второй увеличивает сетку с мелкими подписями RF4.
            passes.Add(
                await RecognizeFileAsync(
                    imagePath,
                    engine,
                    "полный кадр",
                    cancellationToken));

            enlargedGridPath = CreateEnlargedCardGrid(imagePath);
            passes.Add(
                await RecognizeFileAsync(
                    enlargedGridPath,
                    engine,
                    "увеличенная сетка",
                    cancellationToken));

            passes.Add(
                await RecognizeVisibleCardsAsync(
                    imagePath,
                    engine,
                    cancellationToken));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(enlargedGridPath))
            {
                try
                {
                    File.Delete(enlargedGridPath);
                }
                catch (Exception exception)
                {
                    AppLog.Info(
                        "Не удалось удалить временный OCR-файл садка: " +
                        exception.Message);
                }
            }
        }

        // Один и тот же объект читается несколькими OCR-проходами. Берём
        // максимальное число одинаковых карточек в одном проходе: так проходы
        // не создают копии, но две реальные рыбы одного вида и веса остаются.
        var cleanedPasses = passes
            .Select(pass => pass
                .Select(item => item with
                {
                    FishName =
                        KeepnetNameNormalizer.Clean(item.FishName)
                })
                .Where(item => item.FishName.Length >= 3)
                .ToList())
            .ToList();
        var allCandidates = cleanedPasses
            .SelectMany(pass => pass)
            .ToList();
        var canonicalPasses = cleanedPasses
            .Select(pass => pass
                .Select(item => item with
                {
                    FishName = GetCanonicalFishName(
                        item,
                        allCandidates)
                })
                .ToList())
            .ToList();

        var merged = canonicalPasses
            .Select(pass => pass
                .GroupBy(GetRecognitionKey, StringComparer.Ordinal)
                .Select(group => new
                {
                    Key = group.Key,
                    Sample = group.First(),
                    Count = group.Count()
                })
                .ToList())
            .SelectMany(pass => pass)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                var best = group
                    .OrderByDescending(item => item.Count)
                    .First();
                return Enumerable.Repeat(best.Sample, best.Count);
            })
            .Take(MaximumCards)
            .ToList();

        AppLog.Info(
            $"OCR садка завершён. Карточек после сведения проходов: " +
            $"{merged.Count}.");
        return merged;
    }

    private static async Task<IReadOnlyList<RecognizedKeepnetFish>>
        RecognizeVisibleCardsAsync(
            string imagePath,
            OcrEngine engine,
            CancellationToken cancellationToken)
    {
        var imageSets = CreateVisibleCardImages(imagePath);
        var recognized = new List<RecognizedKeepnetFish>();

        try
        {
            foreach (var imageSet in imageSets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lines = new List<string>();
                lines.AddRange(
                    await ReadOcrLinesAsync(
                        imageSet.FullPath,
                        engine));
                lines.AddRange(
                    await ReadOcrLinesAsync(
                        imageSet.LabelPath,
                        engine));
                var card = KeepnetCardParser.Parse(lines);
                if (card is not null)
                {
                    recognized.Add(card);
                }
            }
        }
        finally
        {
            foreach (var imageSet in imageSets)
            {
                foreach (var path in new[]
                         {
                             imageSet.FullPath,
                             imageSet.LabelPath
                         })
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // Временные файлы удалит системная очистка.
                    }
                }
            }
        }

        AppLog.Info(
            $"OCR садка (отдельные карточки): распознано " +
            $"{recognized.Count} из {imageSets.Count} полностью видимых.");
        return recognized;
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
        return result.Lines.Select(line => line.Text).ToList();
    }

    private static IReadOnlyList<CardImageSet> CreateVisibleCardImages(
        string imagePath)
    {
        using var source = new Bitmap(imagePath);
        var cardWidth = (int)Math.Round(source.Width * 0.116);
        var cardHeight = (int)Math.Round(source.Height * 0.163);
        var firstX = (int)Math.Round(source.Width * 0.2135);
        var firstY = (int)Math.Round(source.Height * 0.161);
        var stepX = source.Width * 0.1282;
        var stepY = source.Height * 0.1852;
        var result = new List<CardImageSet>();

        for (var row = 0; row < 10; row++)
        {
            var y = (int)Math.Round(firstY + row * stepY);
            if (y + cardHeight > source.Height - 4)
            {
                break;
            }

            for (var column = 0; column < 6; column++)
            {
                var x = (int)Math.Round(firstX + column * stepX);
                if (x + cardWidth > source.Width)
                {
                    break;
                }

                const double scale = 2.6;
                using var card = new Bitmap(
                    (int)Math.Round(cardWidth * scale),
                    (int)Math.Round(cardHeight * scale),
                    PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(card))
                {
                    graphics.InterpolationMode =
                        InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(
                        source,
                        new Rectangle(0, 0, card.Width, card.Height),
                        new Rectangle(x, y, cardWidth, cardHeight),
                        GraphicsUnit.Pixel);
                }

                var fullPath = Path.Combine(
                    Path.GetTempPath(),
                    $"RF4AssistantPro_card_{Guid.NewGuid():N}.png");
                card.Save(fullPath, ImageFormat.Png);

                var labelY = y + (int)Math.Round(cardHeight * 0.66);
                var labelHeight = Math.Max(
                    1,
                    y + cardHeight - labelY);
                const double labelScale = 4.2;
                using var label = new Bitmap(
                    (int)Math.Round(cardWidth * labelScale),
                    (int)Math.Round(labelHeight * labelScale),
                    PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(label))
                {
                    graphics.InterpolationMode =
                        InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode =
                        PixelOffsetMode.HighQuality;
                    graphics.DrawImage(
                        source,
                        new Rectangle(
                            0,
                            0,
                            label.Width,
                            label.Height),
                        new Rectangle(
                            x,
                            labelY,
                            cardWidth,
                            labelHeight),
                        GraphicsUnit.Pixel);
                }

                var labelPath = Path.Combine(
                    Path.GetTempPath(),
                    $"RF4AssistantPro_label_{Guid.NewGuid():N}.png");
                label.Save(labelPath, ImageFormat.Png);
                result.Add(new CardImageSet(fullPath, labelPath));
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<RecognizedKeepnetFish>>
        RecognizeFileAsync(
            string imagePath,
            OcrEngine engine,
            string passName,
            CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();

        var anchors = FindWeightAnchors(result.Lines).ToList();
        var recognized = ExtractRecognized(
            result,
            decoder.PixelWidth,
            decoder.PixelHeight,
            anchors);
        AppLog.Info(
            $"OCR садка ({passName}): весов найдено {anchors.Count}; " +
            $"карточек распознано {recognized.Count}.");
        AppLog.OcrDetails(
            $"OCR садка ({passName}), полный текст: " +
            string.Join(" | ", result.Lines.Select(line => line.Text)));
        return recognized;
    }

    private static IReadOnlyList<RecognizedKeepnetFish> ExtractRecognized(
        OcrResult result,
        uint pixelWidth,
        uint pixelHeight,
        IEnumerable<WeightAnchor> weightAnchors)
    {
        var allWords = result.Lines
            .SelectMany(line => line.Words)
            .ToList();
        var anchors = weightAnchors
            .OrderBy(anchor => anchor.Top)
            .ThenBy(anchor => anchor.CenterX)
            .Take(MaximumCards)
            .ToList();
        var recognized = new List<RecognizedKeepnetFish>();

        foreach (var anchor in anchors)
        {
            var nearestHorizontalDistance = anchors
                .Where(other =>
                    !ReferenceEquals(other, anchor) &&
                    Math.Abs(other.CenterY - anchor.CenterY) <
                    pixelHeight * 0.04)
                .Select(other => Math.Abs(other.CenterX - anchor.CenterX))
                .DefaultIfEmpty(pixelWidth * 0.13)
                .Min();
            var horizontalRadius = Math.Min(
                pixelWidth * 0.065,
                Math.Max(
                    pixelWidth * 0.025,
                    nearestHorizontalDistance * 0.46));
            var maxNameDistance = pixelHeight * 0.060;

            var candidateWords = allWords
                .Where(word =>
                {
                    var rect = word.BoundingRect;
                    var centerX = rect.X + rect.Width / 2;
                    var centerY = rect.Y + rect.Height / 2;
                    return FishWordRegex.IsMatch(word.Text) &&
                           !UnitRegex.IsMatch(word.Text) &&
                           centerX >= anchor.CenterX - horizontalRadius &&
                           centerX <= anchor.CenterX + horizontalRadius &&
                           centerY >= anchor.Bottom - 3 &&
                           centerY <= anchor.Bottom + maxNameDistance;
                })
                .OrderBy(word => word.BoundingRect.Y)
                .ThenBy(word => word.BoundingRect.X)
                .ToList();

            if (candidateWords.Count == 0)
            {
                continue;
            }

            var firstTop = candidateWords[0].BoundingRect.Y;
            var lineTolerance = Math.Max(8d, pixelHeight * 0.012);
            var fishName = KeepnetNameNormalizer.Clean(
                string.Join(
                    " ",
                    candidateWords
                        .Where(word =>
                            Math.Abs(
                                word.BoundingRect.Y - firstTop) <=
                            lineTolerance)
                        .OrderBy(word => word.BoundingRect.X)
                        .Select(word => word.Text)));
            if (fishName.Length < 3 ||
                !char.IsUpper(fishName[0]))
            {
                continue;
            }

            recognized.Add(new RecognizedKeepnetFish(
                fishName,
                anchor.WeightKg,
                $"{anchor.SourceText}{Environment.NewLine}{fishName}"));
        }

        return recognized;
    }

    private static string CreateEnlargedCardGrid(string imagePath)
    {
        using var source = new Bitmap(imagePath);
        var cropX = Math.Clamp(
            (int)Math.Round(source.Width * 0.195),
            0,
            Math.Max(0, source.Width - 1));
        var cropY = Math.Clamp(
            (int)Math.Round(source.Height * 0.145),
            0,
            Math.Max(0, source.Height - 1));
        var cropWidth = Math.Max(
            1,
            Math.Min(
                source.Width - cropX,
                (int)Math.Round(source.Width * 0.790)));
        var cropHeight = Math.Max(
            1,
            Math.Min(
                source.Height - cropY,
                (int)Math.Round(source.Height * 0.850)));
        var scale = Math.Min(
            1.70d,
            Math.Min(2800d / cropWidth, 2200d / cropHeight));
        scale = Math.Max(1.15d, scale);
        var targetWidth = Math.Max(
            1,
            (int)Math.Round(cropWidth * scale));
        var targetHeight = Math.Max(
            1,
            (int)Math.Round(cropHeight * scale));

        using var enlarged = new Bitmap(
            targetWidth,
            targetHeight,
            PixelFormat.Format24bppRgb);
        enlarged.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(enlarged))
        {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, targetWidth, targetHeight),
                new Rectangle(cropX, cropY, cropWidth, cropHeight),
                GraphicsUnit.Pixel);
        }

        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"RF4AssistantPro_keepnet_{Guid.NewGuid():N}.png");
        enlarged.Save(outputPath, ImageFormat.Png);
        return outputPath;
    }

    private static IEnumerable<WeightAnchor> FindWeightAnchors(
        IReadOnlyList<OcrLine> lines)
    {
        foreach (var line in lines)
        {
            for (var index = 0; index < line.Words.Count; index++)
            {
                var word = line.Words[index];
                var compact = word.Text.Replace(" ", "");
                var match = WeightTokenRegex.Match(compact);
                if (!match.Success ||
                    !decimal.TryParse(
                        match.Groups["value"].Value.Replace(',', '.'),
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var displayedWeight))
                {
                    continue;
                }

                var unit = match.Groups["unit"].Value;
                var right = word.BoundingRect.X + word.BoundingRect.Width;
                var bottom = word.BoundingRect.Y + word.BoundingRect.Height;
                var sourceText = word.Text;

                if (unit.Length == 0 &&
                    index + 1 < line.Words.Count &&
                    UnitRegex.IsMatch(line.Words[index + 1].Text))
                {
                    var unitWord = line.Words[++index];
                    unit = unitWord.Text;
                    right = Math.Max(
                        right,
                        unitWord.BoundingRect.X + unitWord.BoundingRect.Width);
                    bottom = Math.Max(
                        bottom,
                        unitWord.BoundingRect.Y + unitWord.BoundingRect.Height);
                    sourceText = $"{sourceText} {unitWord.Text}";
                }

                if (unit.Length == 0)
                {
                    continue;
                }

                var weightKg = unit.Equals(
                    "г",
                    StringComparison.OrdinalIgnoreCase)
                    ? displayedWeight / 1000m
                    : displayedWeight;
                if (weightKg <= 0m || weightKg > 5000m)
                {
                    continue;
                }

                yield return new WeightAnchor(
                    weightKg,
                    word.BoundingRect.X,
                    word.BoundingRect.Y,
                    right,
                    bottom,
                    sourceText);
            }
        }
    }

    private static string NormalizeForKey(string value)
    {
        return string.Concat(
            value
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character)));
    }

    private static string GetRecognitionKey(
        RecognizedKeepnetFish item)
    {
        return $"{NormalizeForKey(item.FishName)}|" +
               item.WeightKg.ToString(
                   "0.######",
                   CultureInfo.InvariantCulture);
    }

    private static string GetCanonicalFishName(
        RecognizedKeepnetFish item,
        IReadOnlyCollection<RecognizedKeepnetFish> candidates)
    {
        var current = NormalizeForKey(item.FishName);
        return candidates
            .Where(candidate =>
                candidate.WeightKg == item.WeightKg)
            .Select(candidate => candidate.FishName)
            .Where(name =>
            {
                var normalized = NormalizeForKey(name);
                return normalized == current ||
                       normalized.StartsWith(
                           current,
                           StringComparison.Ordinal);
            })
            .OrderByDescending(name => name.Length)
            .FirstOrDefault() ?? item.FishName;
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

    private sealed record WeightAnchor(
        decimal WeightKg,
        double Left,
        double Top,
        double Right,
        double Bottom,
        string SourceText)
    {
        public double CenterX => (Left + Right) / 2;

        public double CenterY => (Top + Bottom) / 2;
    }

    private sealed record CardImageSet(
        string FullPath,
        string LabelPath);
}
