namespace RF4AssistantPro.Cafe;

public static class CafeWaterBodyInference
{
    public static string FindDominant(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .FirstOrDefault() ?? "";
    }

    public static string Resolve(string? cardValue, string? dominantValue) =>
        !string.IsNullOrWhiteSpace(cardValue)
            ? cardValue.Trim()
            : dominantValue?.Trim() ?? "";

    private static string Normalize(string value) => string.Join(
        " ",
        value.Trim()
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
