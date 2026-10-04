namespace RF4AssistantPro.Models;

public static class KeepnetMergePlanner
{
    public static IReadOnlyList<int> PlanCountsToAdd(
        int currentTotal,
        int? expectedTotal,
        IEnumerable<(int ExistingForKey, int ScannedForKey)> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var total = Math.Max(0, currentTotal);
        var result = new List<int>();
        foreach (var group in groups)
        {
            var toAdd = GetCountToAdd(
                group.ExistingForKey,
                group.ScannedForKey,
                total,
                expectedTotal);
            result.Add(toAdd);
            total += toAdd;
        }

        return result;
    }

    public static int GetCountToAdd(
        int existingForKey,
        int scannedForKey,
        int currentTotal,
        int? expectedTotal)
    {
        var missingForKey = Math.Max(0, scannedForKey - existingForKey);
        if (!expectedTotal.HasValue)
        {
            return missingForKey;
        }

        var remainingSlots = Math.Max(0, expectedTotal.Value - currentTotal);
        return Math.Min(missingForKey, remainingSlots);
    }

    public static int GetCountToMirrorToCatches(
        int existingCatchCount,
        int currentKeepnetCount,
        int newlyAddedKeepnetCount)
    {
        var missingInCatches = Math.Max(
            0,
            currentKeepnetCount - existingCatchCount);
        return Math.Min(newlyAddedKeepnetCount, missingInCatches);
    }
}