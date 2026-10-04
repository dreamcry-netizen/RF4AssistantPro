namespace RF4AssistantPro.Cafe;

public sealed class CafeSnapshot
{
    public DateTime CapturedAt { get; init; }

    public string FullImagePath { get; init; } = "";

    public string ThumbnailPath { get; init; } = "";

    public List<CafeOffer> Offers { get; init; } = [];

    public Dictionary<Guid, int> OfferMatchedCounts { get; init; } = [];

    public int MatchedCatchCount { get; init; }
}