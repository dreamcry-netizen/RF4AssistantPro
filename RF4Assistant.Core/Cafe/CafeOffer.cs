namespace RF4AssistantPro.Cafe;

public sealed class CafeOffer
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string FishName { get; init; } = "";

    public string RawFishName { get; init; } = "";

    public string FishCatalogId { get; init; } = "";

    public string RecognitionSource { get; init; } = "";

    public string WaterBodyName { get; init; } = "";

    public int Quantity { get; init; }

    public decimal? MinimumWeightGrams { get; init; }

    public string MinimumWeightUnit { get; init; } = "";

    public string RawWeightText { get; init; } = "";

    public string WeightSource { get; init; } = "";

    public decimal? AlternativeMinimumWeightGrams { get; init; }

    public string AlternativeMinimumWeightUnit { get; init; } = "";

    public string AlternativeRawWeightText { get; init; } = "";

    public string AlternativeWeightSource { get; init; } = "";

    public decimal? Price { get; init; }
}