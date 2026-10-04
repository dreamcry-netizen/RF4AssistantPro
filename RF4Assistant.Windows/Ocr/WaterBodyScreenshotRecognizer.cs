using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Ocr;

public sealed class WaterBodyScreenshotRecognizer
{
    private static readonly Regex PrefixedNameRegex = new(
        @"^(?:р\.?|река|оз\.?|озеро|водо[её]м)\s+.+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] IgnoredLines =
    [
        "условные обозначения",
        "база",
        "лагерь",
        "деревянные сооружения",
        "дерево",
        "мост",
        "заболоченная местность"
    ];

    private static readonly string[] KnownWaterBodies =
    [
        "оз. Комариное",
        "оз. Лосиное",
        "р. Вьюнок",
        "оз. Старый Острог",
        "р. Белая",
        "оз. Куори",
        "р. Волхов",
        "р. Северский Донец",
        "р. Сура",
        "Ладожское оз.",
        "оз. Янтарное",
        "Ладожский архипелаг",
        "р. Ахтуба",
        "оз. Медное",
        "р. Нижняя Тунгуска",
        "р. Яма",
        "Норвежское море"
    ];

    public async Task<RecognizedWaterBody?> RecognizeAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);

        var engine = TryCreateRussianEngine()
            ?? throw new InvalidOperationException(
                "В Windows не установлен OCR-пакет русского языка.");

        // Название водоёма находится в табличке в левом верхнем углу карты.
        var x = (uint)(decoder.PixelWidth * 0.03);
        var y = (uint)(decoder.PixelHeight * 0.03);
        var width = Math.Max(
            1u,
            (uint)(decoder.PixelWidth * 0.36));
        var height = Math.Max(
            1u,
            (uint)(decoder.PixelHeight * 0.20));
        var scale = Math.Min(
            2.0,
            Math.Min(
                OcrEngine.MaxImageDimension / (double)width,
                OcrEngine.MaxImageDimension / (double)height));

        var transform = new BitmapTransform
        {
            Bounds = new BitmapBounds
            {
                X = x,
                Y = y,
                Width = Math.Min(width, decoder.PixelWidth - x),
                Height = Math.Min(height, decoder.PixelHeight - y)
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

        var lines = result.Lines
            .Select(line => Normalize(line.Text))
            .Where(line => line.Length > 0)
            .ToList();
        var rawText = string.Join(Environment.NewLine, lines);
        AppLog.OcrDetails(
            $"OCR водоёма: область={x},{y},{width},{height}; " +
            $"строк={lines.Count}; текст=" +
            rawText.Replace(Environment.NewLine, " | "));

        var name = lines.FirstOrDefault(line =>
                PrefixedNameRegex.IsMatch(line) &&
                IsNameCandidate(line))
            ?? lines.FirstOrDefault(IsNameCandidate);

        if (string.IsNullOrWhiteSpace(name))
        {
            AppLog.Info("OCR водоёма: название не найдено.");
            return null;
        }

        var canonicalName = FindKnownWaterBody(name) ?? name;
        AppLog.Info(
            $"OCR водоёма: распознано «{name}», " +
            $"нормализовано «{canonicalName}».");
        return new RecognizedWaterBody(canonicalName, rawText);
    }

    private static bool IsNameCandidate(string value)
    {
        if (value.Length < 3 ||
            value.Length > 60 ||
            value.Any(char.IsDigit) ||
            !value.Any(char.IsLetter))
        {
            return false;
        }

        return !IgnoredLines.Contains(
            value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? FindKnownWaterBody(string recognized)
    {
        var normalized = NormalizeForComparison(recognized);
        if (normalized.Length < 4)
        {
            return null;
        }

        var best = KnownWaterBodies
            .Select(name => new
            {
                Name = name,
                Distance = LevenshteinDistance(
                    normalized,
                    NormalizeForComparison(name))
            })
            .OrderBy(item => item.Distance)
            .First();

        var allowedDistance = Math.Max(2, normalized.Length / 3);
        return best.Distance <= allowedDistance
            ? best.Name
            : null;
    }

    private static string NormalizeForComparison(string value)
    {
        return new string(value
            .ToLowerInvariant()
            .Where(char.IsLetter)
            .Select(character => character == 'ё' ? 'е' : character)
            .ToArray());
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var leftIndex = 1;
             leftIndex <= left.Length;
             leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1;
                 rightIndex <= right.Length;
                 rightIndex++)
            {
                var substitutionCost =
                    left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
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

public sealed record RecognizedWaterBody(string Name, string RawText);