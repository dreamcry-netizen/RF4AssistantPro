namespace RF4AssistantPro.Cafe;

public sealed record CafeWeightObservation(
    decimal Grams,
    string Unit,
    string RawText,
    string Source);

public sealed record CafeWeightDecision(
    CafeWeightObservation? Selected,
    CafeWeightObservation? Alternative)
{
    public bool HasConflict =>
        Selected is not null &&
        Alternative is not null &&
        Selected.Grams != Alternative.Grams;
}

public static class CafeWeightResolver
{
    public static CafeWeightDecision Resolve(
        CafeWeightObservation? generalOcr,
        CafeWeightObservation? cardOcr)
    {
        if (cardOcr is null)
        {
            return new CafeWeightDecision(generalOcr, null);
        }

        if (generalOcr is null)
        {
            return new CafeWeightDecision(cardOcr, null);
        }

        if (cardOcr.Grams == generalOcr.Grams)
        {
            return new CafeWeightDecision(
                cardOcr with
                {
                    Source = "OCR карточки + общий OCR"
                },
                null);
        }

        return new CafeWeightDecision(cardOcr, generalOcr);
    }
}