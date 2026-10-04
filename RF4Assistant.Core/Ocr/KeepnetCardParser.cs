using System.Globalization;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Ocr;

public static class KeepnetCardParser
{
    private static readonly Regex WeightRegex = new(
        @"(?<!\d)(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>кг|г|r|g)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FishNameRegex = new(
        @"^[А-ЯЁ][А-ЯЁа-яё\-\s]{2,}$",
        RegexOptions.CultureInvariant);

    public static RecognizedKeepnetFish? Parse(IEnumerable<string> sourceLines)
    {
        ArgumentNullException.ThrowIfNull(sourceLines);
        var lines = sourceLines
            .Select(Normalize)
            .Where(line => line.Length > 0)
            .ToList();

        decimal? weightKg = null;
        foreach (var line in lines)
        {
            var match = WeightRegex.Match(line);
            if (!match.Success ||
                !decimal.TryParse(
                    match.Groups["value"].Value.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var displayedWeight))
            {
                continue;
            }

            weightKg = match.Groups["unit"].Value.Equals(
                "кг",
                StringComparison.OrdinalIgnoreCase)
                ? displayedWeight
                : displayedWeight / 1000m;
            break;
        }

        if (weightKg is null or <= 0m or > 5000m)
        {
            return null;
        }

        var fishName = lines
            .Where(IsFishName)
            .OrderByDescending(line => line.Count(char.IsLetter))
            .FirstOrDefault();
        if (fishName is null)
        {
            return null;
        }

        return new RecognizedKeepnetFish(
            fishName,
            weightKg.Value,
            string.Join(Environment.NewLine, lines));
    }

    private static bool IsFishName(string line)
    {
        if (!FishNameRegex.IsMatch(line) ||
            line.Contains("мин", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("час", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("ёмкость", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("садок", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("рыб", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("массе", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("вылова", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !WeightRegex.IsMatch(line);
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}