namespace RF4AssistantPro.Ocr;

public sealed class RecognizedCatch
{
    public string FishName { get; init; } = "";

    public string WaterBodyName { get; init; } = "";

    public decimal WeightKg { get; init; }

    public decimal? LengthCm { get; init; }

    public string Quality { get; init; } = "";

    public string RawText { get; init; } = "";

    public bool NeedsReview { get; init; }

    public string ReviewReason { get; init; } = "";
}