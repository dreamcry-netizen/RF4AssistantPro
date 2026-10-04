namespace RF4AssistantPro.Cafe;

public static class CafeOfferMatcher
{
    public static bool IsWeightEligible(
        decimal fishWeightKg,
        decimal? minimumWeightGrams)
    {
        if (!minimumWeightGrams.HasValue ||
            minimumWeightGrams.Value <= 0m ||
            fishWeightKg <= 0m)
        {
            return false;
        }

        var fishWeightGrams = fishWeightKg * 1000m;

        // Строгая граница кафе:
        // порог - 1 г не подходит; ровно порог и порог + 1 г подходят.
        return fishWeightGrams >= minimumWeightGrams.Value;
    }
}