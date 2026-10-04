using System.Globalization;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Cafe;

public static class CafeQuantityTextParser
{
    private static readonly Regex LabeledQuantityRegex = new(
        @"Количество\s*(?::|-)?\s*(?<value>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex QuantityWithUnitRegex = new(
        @"(?<!\d)(?<value>\d+)\s*шт\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int? TryParse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var normalized = lines
            .Select(Normalize)
            .Where(line => line.Length > 0)
            .ToList();
        if (normalized.Count == 0)
        {
            return null;
        }

        var text = string.Join(" ", normalized);
        var labeled = LabeledQuantityRegex.Match(text);
        if (TryReadPositive(labeled, out var labeledValue))
        {
            return labeledValue;
        }

        var withUnit = QuantityWithUnitRegex.Match(text);
        if (TryReadPositive(withUnit, out var unitValue))
        {
            return unitValue;
        }

        // PaddleOCR may return «Количество», «3» and «шт» as
        // independent blocks. Use the label as the anchor and inspect
        // only the next few tokens, so a weight or a price is not selected.
        var tokens = normalized
            .SelectMany(line => line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!tokens[index].Equals(
                    "Количество",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var offset = 1;
                 offset <= 3 && index + offset < tokens.Length;
                 offset++)
            {
                if (int.TryParse(
                        tokens[index + offset],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var value) &&
                    value > 0)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryReadPositive(
        Match match,
        out int value)
    {
        value = 0;
        return match.Success &&
               int.TryParse(
                   match.Groups["value"].Value,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out value) &&
               value > 0;
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }
}