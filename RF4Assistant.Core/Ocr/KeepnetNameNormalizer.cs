using System.Text.RegularExpressions;

namespace RF4AssistantPro.Ocr;

public static class KeepnetNameNormalizer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+");
    private static readonly Regex TrailingWeightUnitRegex = new(
        @"(?:\s+(?:кг|г))+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Clean(string? value)
    {
        var normalized = WhitespaceRegex
            .Replace(value?.Trim() ?? string.Empty, " ");
        return TrailingWeightUnitRegex
            .Replace(normalized, string.Empty)
            .Trim();
    }
}
