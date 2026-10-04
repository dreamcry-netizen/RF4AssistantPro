using System.Globalization;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Ocr;

public static class CatchTextParser
{
    private static readonly Regex WeightRegex = new(
        @"(?<![\d\-–—])(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>кг|г|r)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LengthRegex = new(
        @"(?<![\d\-–—])(?<value>\d+(?:[.,]\d+)?)\s*см\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FishNameRegex = new(
        @"^[А-ЯЁ][А-ЯЁа-яё\-\s]{2,}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex WaterBodyLabelRegex = new(
        @"^(?:Водо[её]м|Место)\s*:?\s*(?<value>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] IgnoredLines =
    [
        "рыба",
        "бонус",
        "зачётная",
        "незачётная",
        "зачетная",
        "незачетная",
        "премиум",
        "счастливый час",
        "очков опыта",
        "всего очков опыта",
        "в садок",
        "отпустить"
    ];

    public static RecognizedCatch? Parse(
        IEnumerable<string> sourceLines,
        IEnumerable<string>? preferredNameLines = null)
    {
        ArgumentNullException.ThrowIfNull(sourceLines);

        var lines = sourceLines
            .Select(NormalizeLine)
            .Where(line => line.Length > 0)
            .ToList();
        var rawText = string.Join(Environment.NewLine, lines);

        var length = FindLength(lines);
        if (length is not null &&
            (length.Value.Value <= 0m || length.Value.Value > 2000m))
        {
            return null;
        }

        var weight = length is null
            ? FindFirstWeight(lines)
            : FindNearestWeight(lines, length.Value.LineIndex);
        if (weight is null || weight.Value <= 0m || weight.Value > 5000m)
        {
            return null;
        }

        var preferredNames = preferredNameLines?
            .Select(NormalizeFishNameLine)
            .Where(line => line.Length > 0)
            .ToList() ?? [];

        var fishName = SelectBestFishName(preferredNames)
            ?? SelectBestFishName(lines)
            ?? "";
        if (fishName.Length == 0)
        {
            return null;
        }

        var waterBodyName = lines
            .Select(TryExtractWaterBodyName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "";
        var quality = lines.FirstOrDefault(line =>
            line.Contains("зач", StringComparison.OrdinalIgnoreCase)) ?? "";

        return new RecognizedCatch
        {
            FishName = fishName,
            WaterBodyName = waterBodyName,
            WeightKg = weight.Value,
            LengthCm = length?.Value,
            Quality = quality,
            RawText = rawText
        };
    }

    private static (decimal Value, int LineIndex)? FindLength(
        IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var match = LengthRegex.Match(lines[index]);
            if (TryReadDecimal(match, out var value))
            {
                return (value, index);
            }
        }

        return null;
    }

    private static decimal? FindNearestWeight(
        IReadOnlyList<string> lines,
        int lengthLineIndex)
    {
        var candidates = new List<(decimal WeightKg, int Distance, int Index)>();
        for (var index = 0; index < lines.Count; index++)
        {
            foreach (Match match in WeightRegex.Matches(lines[index]))
            {
                if (!TryReadDecimal(match, out var displayedWeight))
                {
                    continue;
                }

                var weightKg = IsKilograms(match.Groups["unit"].Value)
                    ? displayedWeight
                    : displayedWeight / 1000m;
                candidates.Add((
                    weightKg,
                    Math.Abs(index - lengthLineIndex),
                    index));
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => (decimal?)candidate.WeightKg)
            .FirstOrDefault();
    }

    private static decimal? FindFirstWeight(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            foreach (Match match in WeightRegex.Matches(lines[index]))
            {
                if (!TryReadDecimal(match, out var displayedWeight))
                {
                    continue;
                }

                return IsKilograms(match.Groups["unit"].Value)
                    ? displayedWeight
                    : displayedWeight / 1000m;
            }
        }

        return null;
    }

    private static bool TryReadDecimal(Match match, out decimal value)
    {
        value = 0m;
        return match.Success &&
               decimal.TryParse(
                   match.Groups["value"].Value.Replace(',', '.'),
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private static bool IsKilograms(string unit)
    {
        return unit.Equals("кг", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFishNameCandidate(string line)
    {
        return FishNameRegex.IsMatch(line) &&
               !IgnoredLines.Contains(line, StringComparer.OrdinalIgnoreCase) &&
               !line.Contains("см", StringComparison.OrdinalIgnoreCase) &&
               !line.Contains("кг", StringComparison.OrdinalIgnoreCase) &&
               !line.Contains(" г", StringComparison.OrdinalIgnoreCase) &&
               !IsLikelyTruncatedName(line);
    }

    private static string? SelectBestFishName(IEnumerable<string> lines)
    {
        return lines
            .Where(IsFishNameCandidate)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(GetFishNameScore)
            .FirstOrDefault();
    }

    private static int GetFishNameScore(string value)
    {
        var letters = value.Count(char.IsLetter);
        var hasLowerCase = value.Any(char.IsLower);
        var wordCount = value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries).Length;
        return letters + wordCount * 4 + (hasLowerCase ? 20 : 0);
    }

    private static bool IsLikelyTruncatedName(string value)
    {
        var letters = value.Where(char.IsLetter).ToList();
        return letters.Count <= 5 &&
               letters.Count > 0 &&
               letters.All(char.IsUpper);
    }

    private static string NormalizeLine(string line)
    {
        return string.Join(
            " ",
            line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeFishNameLine(string line)
    {
        var normalized = NormalizeLine(line);
        var parts = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        // При сильном увеличении Windows OCR иногда разделяет одно слово:
        // «Ел е ц». Склеиваем только набор коротких фрагментов, чтобы не
        // повредить обычные составные названия рыб.
        if (parts.Length >= 2 &&
            parts.All(part => part.Length <= 2) &&
            parts.Sum(part => part.Length) <= 20)
        {
            var joined = string.Concat(parts);
            return joined.Length == 0
                ? joined
                : char.ToUpperInvariant(joined[0]) +
                  joined[1..].ToLowerInvariant();
        }

        return normalized;
    }

    private static string TryExtractWaterBodyName(string line)
    {
        var labeledMatch = WaterBodyLabelRegex.Match(line);
        if (labeledMatch.Success)
        {
            return labeledMatch.Groups["value"].Value.Trim();
        }

        if (line.StartsWith("р.", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("река ", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("оз.", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("озеро ", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("море", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("архипелаг", StringComparison.OrdinalIgnoreCase))
        {
            return line;
        }

        return "";
    }
}