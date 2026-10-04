namespace RF4AssistantPro.Baits;

public sealed class BaitCatalogMatcher
{
    private readonly BaitCatalogStore _store;

    public BaitCatalogMatcher(BaitCatalogStore store)
    {
        _store = store;
    }

    public BaitMatch? Find(string candidate, string rawText)
    {
        var normalizedCandidate = BaitMatchScorer.Normalize(candidate);
        var normalizedRawText = BaitMatchScorer.Normalize(rawText);
        if (normalizedCandidate.Length < 2)
        {
            return null;
        }

        BaitMatch? best = null;
        foreach (var item in _store.LoadCatalog().Where(item => item.IsEnabled))
        {
            foreach (var alias in GetAliases(item))
            {
                var normalizedAlias = BaitMatchScorer.Normalize(alias);
                if (normalizedAlias.Length < 2)
                {
                    continue;
                }

                var score = BaitMatchScorer.Score(
                    normalizedCandidate,
                    normalizedAlias);

                // Полный OCR используется только для составного имени
                // «производитель + название». Описание типа не должно
                // ошибочно становиться конкретной наживкой.
                var fullName = BaitMatchScorer.Normalize(item.DisplayName);
                if (fullName.Contains(' ') &&
                    normalizedRawText.Contains(fullName))
                {
                    score = Math.Max(score, 0.96);
                }

                if (best is null ||
                    score > best.Confidence ||
                    Math.Abs(score - best.Confidence) < 0.000001 &&
                    item.DisplayName.Length > best.Item.DisplayName.Length)
                {
                    best = new BaitMatch(item, score);
                }
            }
        }

        return best is { Confidence: >= 0.62 } ? best : null;
    }

    private static IEnumerable<string> GetAliases(BaitCatalogItem item)
    {
        yield return item.Name;
        yield return item.DisplayName;

        foreach (var alias in item.Aliases.Split(
                     [';', ',', '\n'],
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            yield return alias;
        }
    }

}

public sealed record BaitMatch(BaitCatalogItem Item, double Confidence);