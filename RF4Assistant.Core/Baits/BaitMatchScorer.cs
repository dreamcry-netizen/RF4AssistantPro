using System.Text;

namespace RF4AssistantPro.Baits;

public static class BaitMatchScorer
{
    public static double Score(string candidate, string alias)
    {
        var normalizedCandidate = Normalize(candidate);
        var normalizedAlias = Normalize(alias);
        if (normalizedCandidate.Length < 2 || normalizedAlias.Length < 2)
        {
            return 0d;
        }

        if (normalizedCandidate.Equals(
                normalizedAlias,
                StringComparison.Ordinal))
        {
            return 1d;
        }

        // Частичное совпадение больше не считается точным. Более длинное и
        // конкретное название получает больший балл: «Червь навозный»
        // выигрывает у общего «Червь».
        if (normalizedCandidate.Contains(
                normalizedAlias,
                StringComparison.Ordinal))
        {
            return 0.80 +
                   0.19 * normalizedAlias.Length /
                   normalizedCandidate.Length;
        }

        if (normalizedAlias.Contains(
                normalizedCandidate,
                StringComparison.Ordinal))
        {
            return 0.75 +
                   0.18 * normalizedCandidate.Length /
                   normalizedAlias.Length;
        }

        return Similarity(normalizedCandidate, normalizedAlias);
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant().Replace('ё', 'е'))
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            " ",
            builder.ToString().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var distance = Levenshtein(left, right);
        return 1d - distance / (double)Math.Max(left.Length, right.Length);
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] +
                    (left[i - 1] == right[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}