using System.Globalization;

namespace RF4AssistantPro.Cafe;

public sealed record CafeOfferReviewInput(
    string FishName,
    string WaterBodyName,
    string Quantity,
    string MinimumWeight,
    string WeightUnit,
    decimal? Price = null,
    string WeightSource = "",
    string RawWeightText = "",
    decimal? AlternativeMinimumWeightGrams = null,
    string AlternativeMinimumWeightUnit = "",
    string AlternativeRawWeightText = "",
    string AlternativeWeightSource = "",
    Guid Id = default,
    string RawFishName = "",
    string FishCatalogId = "",
    string RecognitionSource = "");

public static class CafeOfferReviewParser
{
    public static bool TryCreate(
        CafeOfferReviewInput input,
        out CafeOffer? offer,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(input);
        offer = null;
        error = "";

        var enteredFishName = input.FishName.Trim();
        var normalizedFish = CafeFishNameCanonicalizer.Canonicalize(
            enteredFishName,
            []);
        var fishName = normalizedFish.Name;
        if (fishName.Length < 2)
        {
            error = "укажите название рыбы";
            return false;
        }

        if (!int.TryParse(
                input.Quantity.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var quantity) ||
            quantity <= 0)
        {
            error = "количество должно быть целым числом больше нуля";
            return false;
        }

        if (!decimal.TryParse(
                input.MinimumWeight.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var displayedWeight) ||
            displayedWeight <= 0m)
        {
            error = "укажите положительный минимальный вес";
            return false;
        }

        var unit = input.WeightUnit.Trim().ToLowerInvariant();
        if (unit is not ("г" or "кг"))
        {
            error = "выберите единицу веса: г или кг";
            return false;
        }

        var weightGrams = unit == "кг"
            ? displayedWeight * 1000m
            : displayedWeight;
        if (weightGrams > 1_000_000m)
        {
            error = "минимальный вес выглядит нереалистично";
            return false;
        }

        offer = new CafeOffer
        {
            Id = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id,
            FishName = fishName,
            RawFishName = string.IsNullOrWhiteSpace(input.RawFishName)
                ? enteredFishName
                : input.RawFishName.Trim(),
            FishCatalogId = input.FishCatalogId.Trim(),
            RecognitionSource = input.RecognitionSource.Trim(),
            WaterBodyName = input.WaterBodyName.Trim(),
            Quantity = quantity,
            MinimumWeightGrams = weightGrams,
            MinimumWeightUnit = unit,
            RawWeightText = input.RawWeightText.Trim(),
            WeightSource = string.IsNullOrWhiteSpace(input.WeightSource)
                ? "подтверждено пользователем"
                : $"подтверждено пользователем: {input.WeightSource.Trim()}",
            AlternativeMinimumWeightGrams =
                input.AlternativeMinimumWeightGrams,
            AlternativeMinimumWeightUnit =
                input.AlternativeMinimumWeightUnit.Trim(),
            AlternativeRawWeightText =
                input.AlternativeRawWeightText.Trim(),
            AlternativeWeightSource =
                input.AlternativeWeightSource.Trim(),
            Price = input.Price
        };
        return true;
    }
}