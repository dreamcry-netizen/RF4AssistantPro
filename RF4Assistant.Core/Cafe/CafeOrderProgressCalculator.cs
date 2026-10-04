namespace RF4AssistantPro.Cafe;

public sealed record CafeOrderProgress(
    int TotalRequested,
    int Matched,
    int Remaining,
    int UnknownQuantityOfferCount)
{
    public IReadOnlyList<CafeOfferProgress> Offers { get; init; } = [];

    public bool HasKnownQuantity => TotalRequested > 0;

    public bool IsCompleted =>
        HasKnownQuantity &&
        Remaining == 0 &&
        UnknownQuantityOfferCount == 0 &&
        Offers
            .Where(offer => offer.HasKnownQuantity)
            .All(offer => offer.IsCompleted);
}

public static class CafeOrderProgressCalculator
{
    public static CafeOrderProgress Calculate(CafeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var totalRequested = snapshot.Offers
            .Where(offer => offer.Quantity > 0)
            .Sum(offer => offer.Quantity);
        var unknownQuantityOfferCount = snapshot.Offers.Count(
            offer => offer.Quantity <= 0);

        var matchedCounts = snapshot.OfferMatchedCounts;
        var legacyRemaining = Math.Max(0, snapshot.MatchedCatchCount);
        var offerProgress = new List<CafeOfferProgress>();
        foreach (var offer in snapshot.Offers)
        {
            var offerMatched = matchedCounts.TryGetValue(
                offer.Id,
                out var storedMatched)
                ? Math.Max(0, storedMatched)
                : Math.Min(
                    Math.Max(0, offer.Quantity),
                    legacyRemaining);
            if (!matchedCounts.ContainsKey(offer.Id) &&
                offer.Quantity > 0)
            {
                legacyRemaining = Math.Max(0, legacyRemaining - offerMatched);
            }

            var knownQuantity = offer.Quantity > 0;
            var requested = knownQuantity ? offer.Quantity : 0;
            var offerRemaining = knownQuantity
                ? Math.Max(0, requested - offerMatched)
                : 0;
            offerProgress.Add(new CafeOfferProgress(
                offer.Id,
                offer.FishName,
                requested,
                Math.Min(offerMatched, requested),
                offerRemaining,
                knownQuantity));
        }

        var matched = offerProgress.Sum(offer =>
            Math.Min(
                offer.Matched,
                Math.Max(0, offer.Requested)));
        var remaining = Math.Max(0, totalRequested - matched);

        return new CafeOrderProgress(
            totalRequested,
            matched,
            remaining,
            unknownQuantityOfferCount)
        {
            Offers = offerProgress
        };
    }
}