using RF4AssistantPro.Ocr;

namespace RF4AssistantPro.Models;

public static class KeepnetReviewPlanner
{
    public static KeepnetRecord ApplyCorrection(
        KeepnetRecord source,
        string fishName,
        decimal weightKg)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cleanName = KeepnetNameNormalizer.Clean(fishName);
        if (cleanName.Length < 3)
        {
            throw new ArgumentException(
                "Название рыбы должно содержать не менее трёх символов.",
                nameof(fishName));
        }

        if (weightKg <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weightKg),
                "Вес должен быть больше нуля.");
        }

        return source with
        {
            FishName = cleanName,
            WeightKg = weightKg,
            NeedsReview = false
        };
    }
}
