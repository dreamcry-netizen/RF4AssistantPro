namespace RF4AssistantPro.Cafe;

public static class CafeCardSlotEvidence
{
    public static bool ShouldInclude(
        CafeCardSlot slot,
        bool hasParsedOffer,
        bool hasVerifiedFields)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return hasParsedOffer ||
               hasVerifiedFields ||
               slot.RawLines.Any(line => !string.IsNullOrWhiteSpace(line));
    }
}
