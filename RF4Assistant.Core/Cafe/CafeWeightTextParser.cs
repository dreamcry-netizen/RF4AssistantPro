using System.Globalization;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Cafe;

public sealed record CafeParsedWeight(
    decimal Grams,
    string Unit,
    string RawText);

public static class CafeWeightTextParser
{
    private static readonly Regex WeightRegex = new(
        @"Масса\s+от\s+(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>г|кг)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LooseWeightRegex = new(
        @"(?<!\d)(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>кг|г|r)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static CafeParsedWeight? TryParse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var normalizedLines = lines
            .Select(line => Regex.Replace(
                line,
                @"(?<=\d)\s+(?=\d{3}\s*(?:кг|КГ|Кг|кГ)\b)",
                ","))
            .ToList();
        var candidates = new List<string>(normalizedLines);
        for (var start = 0; start < normalizedLines.Count; start++)
        {
            for (var length = 2;
                 length <= 4 && start + length <= normalizedLines.Count;
                 length++)
            {
                candidates.Add(string.Join(
                    " ",
                    normalizedLines
                        .Skip(start)
                        .Take(length)));
            }
        }

        var weightMatch = candidates
            .Select(line => WeightRegex.Match(line))
            .FirstOrDefault(match => match.Success);
        weightMatch ??= candidates
            .Select(line => LooseWeightRegex.Match(line))
            .FirstOrDefault(match => match.Success);
        if (weightMatch is null ||
            !weightMatch.Success ||
            !decimal.TryParse(
                weightMatch.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var minimumWeight))
        {
            return null;
        }

        var unit = weightMatch.Groups["unit"].Value.Equals(
            "кг",
            StringComparison.OrdinalIgnoreCase)
            ? "кг"
            : "г";
        return new CafeParsedWeight(
            unit == "кг"
                ? minimumWeight * 1000m
                : minimumWeight,
            unit,
            weightMatch.Value);
    }
}
