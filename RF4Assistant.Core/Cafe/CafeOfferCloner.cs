namespace RF4AssistantPro.Cafe;

public static class CafeOfferCloner
{
    public static CafeOffer WithId(CafeOffer source, Guid id)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new CafeOffer
        {
            Id = id,
            FishName = source.FishName,
            RawFishName = source.RawFishName,
            FishCatalogId = source.FishCatalogId,
            RecognitionSource = source.RecognitionSource,
            WaterBodyName = source.WaterBodyName,
            Quantity = source.Quantity,
            MinimumWeightGrams = source.MinimumWeightGrams,
            MinimumWeightUnit = source.MinimumWeightUnit,
            RawWeightText = source.RawWeightText,
            WeightSource = source.WeightSource,
            AlternativeMinimumWeightGrams =
                source.AlternativeMinimumWeightGrams,
            AlternativeMinimumWeightUnit =
                source.AlternativeMinimumWeightUnit,
            AlternativeRawWeightText =
                source.AlternativeRawWeightText,
            AlternativeWeightSource = source.AlternativeWeightSource,
            Price = source.Price
        };
    }
}