namespace RF4AssistantPro.Cafe;

public sealed record CafeOfferProgress(
    Guid OfferId,
    string FishName,
    int Requested,
    int Matched,
    int Remaining,
    bool HasKnownQuantity)
{
    public bool IsCompleted =>
        HasKnownQuantity &&
        Remaining == 0;
}