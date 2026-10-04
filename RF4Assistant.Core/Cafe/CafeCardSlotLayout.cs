namespace RF4AssistantPro.Cafe;

public sealed record CafePositionedLine(
    string Text,
    double CenterX,
    double CenterY);

public sealed record CafeCardSlot(
    int Index,
    IReadOnlyList<string> RawLines);

public static class CafeCardSlotLayout
{
    public const int SlotCount = 10;

    public static IReadOnlyList<CafeCardSlot> Build(
        IEnumerable<CafePositionedLine> source,
        double imageWidth,
        double imageHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth));
        }

        var slots = Enumerable.Range(0, SlotCount)
            .Select(_ => new List<string>())
            .ToArray();
        foreach (var line in source
                     .OrderBy(item => item.CenterY)
                     .ThenBy(item => item.CenterX))
        {
            var x = line.CenterX / imageWidth;
            var y = line.CenterY / imageHeight;
            if (x < 0.22)
            {
                continue;
            }

            var column = Math.Clamp((int)((x - 0.22) / 0.15), 0, 4);
            var row = y < 0.5 ? 0 : 1;
            var text = Normalize(line.Text);
            if (text.Length > 0)
            {
                slots[row * 5 + column].Add(text);
            }
        }

        return slots
            .Select((lines, index) => new CafeCardSlot(index, lines))
            .ToList();
    }

    private static string Normalize(string text) => string.Join(
        " ",
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
