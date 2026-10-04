using System.Text;

namespace RF4AssistantPro.Fish;

/// <summary>
/// Resolves a catalog image for a catch or keepnet fish name.
/// </summary>
public static class FishImageResolver
{
    public static string Resolve(
        IEnumerable<FishCatalogItem> catalog,
        string fishName)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var normalizedName = NormalizeName(fishName);
        if (normalizedName.Length == 0)
        {
            return "";
        }

        var card = catalog.FirstOrDefault(item =>
            NormalizeName(item.Name) == normalizedName ||
            item.Aliases
                .Split(
                    [';', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Any(alias => NormalizeName(alias) == normalizedName));

        if (card is null || string.IsNullOrWhiteSpace(card.ImagePath))
        {
            return "";
        }

        var path = NormalizeImagePath(card.ImagePath);
        return File.Exists(path) ? path : "";
    }

    private static string NormalizeImagePath(string value)
    {
        var path = value.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            uri.IsFile)
        {
            return uri.LocalPath;
        }

        return path;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var normalized = value.Normalize(NormalizationForm.FormKC);
        return string.Concat(
            normalized
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit));
    }
}