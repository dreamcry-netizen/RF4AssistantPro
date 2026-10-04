using System.Text;
using RF4AssistantPro.Fish;

namespace RF4AssistantPro.Cafe;

public sealed record CafeFishNameMatch(
    string Name,
    string CatalogId,
    string RawName,
    bool IsCanonicalized);

public static class CafeFishNameCanonicalizer
{
    private static readonly IReadOnlyDictionary<string, string> KnownCorrections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rotan"] = "Ротан",
            ["potah"] = "Ротан",
            ["ротан"] = "Ротан",
            ["fustera"] = "Густера",
            ["gustera"] = "Густера",
            ["ryctepa"] = "Густера",
            ["фустера"] = "Густера",
            ["густера"] = "Густера",
            ["карасьззолотой"] = "Карась золотой",
            ["карасьссеребряный"] = "Карась серебряный",
            ["доросомасеверная"] = "Доросома северная",
            ["пескарьобыкновенный"] = "Пескарь обыкновенный"
        };

    public static CafeFishNameMatch Canonicalize(
        string source,
        IEnumerable<FishCatalogItem> catalog,
        IReadOnlyDictionary<string, string>? learnedAliases = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var raw = NormalizeSpacing(source);
        if (raw.Length == 0)
        {
            return new CafeFishNameMatch("", "", "", false);
        }

        var normalized = NormalizeKey(raw);
        var compact = CompactKey(raw);
        var rawCompact = RawCompactKey(raw);
        var enabled = catalog.Where(item => item.IsEnabled).ToList();
        var learnedName = learnedAliases?
            .Where(item =>
                RawCompactKey(item.Key) == rawCompact ||
                CompactKey(item.Key) == compact)
            .Select(item => item.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(learnedName))
        {
            var learnedFish = enabled.FirstOrDefault(item =>
                Names(item).Any(candidate =>
                    CompactKey(candidate) == CompactKey(learnedName)));
            return learnedFish is null
                ? new CafeFishNameMatch(
                    learnedName,
                    "",
                    raw,
                    !string.Equals(learnedName, raw, StringComparison.Ordinal))
                : Match(learnedFish, raw);
        }

        if (KnownCorrections.TryGetValue(rawCompact, out var correctedName) ||
            KnownCorrections.TryGetValue(compact, out correctedName))
        {
            var catalogFish = enabled.FirstOrDefault(item =>
                Names(item).Any(candidate =>
                    CompactKey(candidate) == CompactKey(correctedName)));
            return catalogFish is null
                ? new CafeFishNameMatch(
                    correctedName,
                    "",
                    raw,
                    !string.Equals(correctedName, raw, StringComparison.Ordinal))
                : Match(catalogFish, raw);
        }
        foreach (var fish in enabled)
        {
            foreach (var candidate in Names(fish))
            {
                if (NormalizeKey(candidate) != normalized &&
                    CompactKey(candidate) != compact)
                {
                    continue;
                }

                return Match(fish, raw);
            }
        }

        var fuzzy = enabled
            .SelectMany(fish => Names(fish).Select(candidate => new
            {
                Fish = fish,
                Distance = EditDistance(compact, CompactKey(candidate))
            }))
            .Where(item => item.Distance <= AllowedDistance(compact.Length))
            .OrderBy(item => item.Distance)
            .ToList();
        if (fuzzy.Count > 0 &&
            (fuzzy.Count == 1 || fuzzy[0].Distance < fuzzy[1].Distance))
        {
            return Match(fuzzy[0].Fish, raw);
        }

        return new CafeFishNameMatch(raw, "", raw, false);
    }

    public static bool NamesMatch(string left, string right) =>
        NormalizeKey(left) == NormalizeKey(right) ||
        CompactKey(left) == CompactKey(right);

    private static IEnumerable<string> Names(FishCatalogItem item)
    {
        yield return item.Name;
        foreach (var alias in item.Aliases.Split(
                     [';', ',', '\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            yield return alias;
        }
    }

    private static string NormalizeSpacing(string value) => string.Join(
        " ",
        (value ?? "").Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));

    private static string RawCompactKey(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in NormalizeSpacing(value)
                     .ToLowerInvariant()
                     .Replace('ё', 'е'))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string NormalizeKey(string value)
    {
        var normalized = NormalizeSpacing(value)
            .Trim()
            .ToLowerInvariant()
            .Replace('ё', 'е');
        return normalized
            .Replace('a', 'а')
            .Replace('b', 'в')
            .Replace('c', 'с')
            .Replace('e', 'е')
            .Replace('h', 'н')
            .Replace('k', 'к')
            .Replace('m', 'м')
            .Replace('o', 'о')
            .Replace('p', 'р')
            .Replace('r', 'р')
            .Replace('n', 'н')
            .Replace('t', 'т')
            .Replace('x', 'х')
            .Replace('y', 'у');
    }


    private static CafeFishNameMatch Match(
        FishCatalogItem fish,
        string raw) => new(
            fish.Name.Trim(),
            fish.Id,
            raw,
            !string.Equals(fish.Name.Trim(), raw, StringComparison.Ordinal));

    private static int AllowedDistance(int length) => length switch
    {
        < 5 => 0,
        < 12 => 1,
        _ => 2
    };

    private static int EditDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            }

            previous = current;
        }

        return previous[right.Length];
    }

    private static string CompactKey(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in NormalizeKey(value))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
