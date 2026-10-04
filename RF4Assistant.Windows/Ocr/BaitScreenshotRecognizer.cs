using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Ocr;

public sealed class BaitScreenshotRecognizer
{
    private static readonly Regex QuantityRegex = new(
        @"(?:Кол-?во|Количество)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RequiredRegex = new(
        @"Обязательн\w*\s+компонент",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypeRegex = new(
        @"Тип\s*:\s*(?:Насеком|Черв|Рыб|Бойл|Тесто|Сыр|Кукуруз|Горох|Ягод)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AnyTypeRegex = new(
        @"\bТип\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PlaceholderRegex = new(
        @"^Наживка$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] IgnoredNames =
    [
        "наживка",
        "дип",
        "пва-стик",
        "пва-стрингер",
        "оснастка",
        "монтаж",
        "грузило",
        "обыкновенный поводок",
        "поводок"
    ];

    public async Task<RecognizedBait?> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);

        var engine = TryCreateRussianEngine()
            ?? throw new InvalidOperationException(
                "В Windows не установлен OCR-пакет русского языка.");

        // Большая область в журнале показала, что Windows OCR прекращал
        // распознавание на строке крючка и не доходил до наживки. Поэтому
        // сначала читаем увеличенные нижнюю и среднюю полосы отдельно.
        var regions = new[]
        {
            new OcrRegion("нижняя", 0.48, 0.47, 0.51, 0.36, 2.2),
            new OcrRegion("средняя", 0.48, 0.18, 0.51, 0.42, 2.0),
            new OcrRegion("правая целиком", 0.45, 0.04, 0.55, 0.90, 1.6)
        };

        var allRawText = new List<string>();
        foreach (var region in regions)
        {
            var lines = await RecognizeRegionAsync(
                decoder,
                engine,
                region,
                cancellationToken);
            var rawText = string.Join(Environment.NewLine, lines);
            allRawText.Add($"[{region.Name}]{Environment.NewLine}{rawText}");

            AppLog.OcrDetails(
                $"OCR наживки, область «{region.Name}»: строк={lines.Count}; " +
                $"текст={rawText.Replace(Environment.NewLine, " | ")}.");

            var baitName = FindNameBefore(lines, QuantityRegex)
                ?? FindNameBefore(lines, TypeRegex)
                ?? FindNameBefore(lines, RequiredRegex)
                ?? FindNameBefore(lines, PlaceholderRegex);

            if (!string.IsNullOrWhiteSpace(baitName))
            {
                AppLog.Info(
                    $"OCR наживки: выбрано «{baitName}» " +
                    $"из области «{region.Name}».");
                return new RecognizedBait(
                    baitName,
                    string.Join(
                        Environment.NewLine,
                        allRawText));
            }
        }

        AppLog.Info("OCR наживки: подходящее название не найдено.");
        return null;
    }

    private static async Task<IReadOnlyList<string>> RecognizeRegionAsync(
        BitmapDecoder decoder,
        OcrEngine engine,
        OcrRegion region,
        CancellationToken cancellationToken)
    {
        var x = (uint)(decoder.PixelWidth * region.X);
        var y = (uint)(decoder.PixelHeight * region.Y);
        var width = Math.Max(
            1u,
            Math.Min(
                (uint)(decoder.PixelWidth * region.Width),
                decoder.PixelWidth - x));
        var height = Math.Max(
            1u,
            Math.Min(
                (uint)(decoder.PixelHeight * region.Height),
                decoder.PixelHeight - y));
        var scale = Math.Min(
            region.Scale,
            Math.Min(
                OcrEngine.MaxImageDimension / (double)width,
                OcrEngine.MaxImageDimension / (double)height));

        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            },
            ScaledWidth = Math.Max(1u, (uint)(width * scale)),
            ScaledHeight = Math.Max(1u, (uint)(height * scale))
        };

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        cancellationToken.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();

        return result.Lines
            .Select(line => Normalize(line.Text))
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static string? FindNameBefore(
        IReadOnlyList<string> lines,
        Regex markerRegex)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!markerRegex.IsMatch(line))
            {
                continue;
            }

            // Иногда OCR объединяет имя и описание в одну строку.
            var marker = markerRegex.Match(line);
            var typeMarker = AnyTypeRegex.Match(line);
            var nameEnd = typeMarker.Success &&
                          typeMarker.Index < marker.Index
                ? typeMarker.Index
                : marker.Index;
            var sameLineName = line[..nameEnd].Trim(' ', '-', '—');
            if (IsNameCandidate(sameLineName))
            {
                return sameLineName;
            }

            for (var previous = index - 1;
                 previous >= 0 && previous >= index - 5;
                 previous--)
            {
                if (IsNameCandidate(lines[previous]))
                {
                    return lines[previous];
                }
            }
        }

        return null;
    }

    private static bool IsNameCandidate(string value)
    {
        if (value.Length < 2 ||
            value.Length > 80 ||
            value.Contains(':') ||
            !value.Any(char.IsLetter))
        {
            return false;
        }

        var normalized = value.ToLowerInvariant();
        return !IgnoredNames.Any(ignored =>
            normalized.Equals(ignored, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(
                $"{ignored} ",
                StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
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

    private sealed record OcrRegion(
        string Name,
        double X,
        double Y,
        double Width,
        double Height,
        double Scale);
}

public sealed record RecognizedBait(
    string Name,
    string RawText,
    string CatalogItemId = "",
    string ImagePath = "",
    double MatchConfidence = 0);