namespace RF4AssistantPro.Cafe;

public sealed record CafeOcrBlock(
    string Text,
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public double CenterX => (Left + Right) / 2.0;
    public double CenterY => (Top + Bottom) / 2.0;
    public double Height => Math.Max(1.0, Bottom - Top);
}

public sealed record CafeOcrComposedLine(
    string Text,
    double CenterX,
    double CenterY);

public static class CafeOcrTextLayout
{
    public static IReadOnlyList<CafeOcrComposedLine> ComposeLines(
        IEnumerable<CafeOcrBlock> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var blocks = source
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .OrderBy(item => item.CenterY)
            .ThenBy(item => item.CenterX)
            .ToList();
        var rows = new List<List<CafeOcrBlock>>();

        foreach (var block in blocks)
        {
            var row = rows
                .Select(items => new
                {
                    Items = items,
                    Score = VerticalScore(items, block)
                })
                .Where(item => item.Score >= 0.35)
                .OrderByDescending(item => item.Score)
                .FirstOrDefault();
            if (row is null)
            {
                rows.Add([block]);
            }
            else
            {
                row.Items.Add(block);
            }
        }

        return rows
            .Select(items =>
            {
                var ordered = items.OrderBy(item => item.Left).ToList();
                return new CafeOcrComposedLine(
                    string.Join(" ", ordered.Select(item => Normalize(item.Text))),
                    ordered.Average(item => item.CenterX),
                    ordered.Average(item => item.CenterY));
            })
            .Where(line => line.Text.Length > 0)
            .OrderBy(line => line.CenterY)
            .ThenBy(line => line.CenterX)
            .ToList();
    }

    private static double VerticalScore(
        IReadOnlyList<CafeOcrBlock> row,
        CafeOcrBlock block)
    {
        var top = row.Min(item => item.Top);
        var bottom = row.Max(item => item.Bottom);
        var left = row.Min(item => item.Left);
        var right = row.Max(item => item.Right);
        var horizontalGap = block.Left > right
            ? block.Left - right
            : left > block.Right
                ? left - block.Right
                : 0.0;
        var rowHeight = Math.Max(1.0, bottom - top);
        if (horizontalGap > Math.Max(rowHeight, block.Height) * 4.0)
        {
            return -1.0;
        }

        var overlap = Math.Max(0.0, Math.Min(bottom, block.Bottom) -
            Math.Max(top, block.Top));
        var overlapScore = overlap / Math.Min(
            Math.Max(1.0, bottom - top),
            block.Height);
        var rowCenter = (top + bottom) / 2.0;
        var tolerance = Math.Max(bottom - top, block.Height) * 0.65;
        var centerScore = tolerance <= 0.0
            ? 0.0
            : 1.0 - Math.Abs(rowCenter - block.CenterY) / tolerance;
        return Math.Max(overlapScore, centerScore);
    }

    private static string Normalize(string text) => string.Join(
        " ",
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
