using System.Text.Json.Serialization;
using RF4AssistantPro.Models;

namespace RF4AssistantPro.Fish;

public sealed class FishCatalogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string Family { get; set; } = "";

    public string Habitat { get; set; } = "";

    public decimal? TrophyWeightGrams { get; set; }

    public string Aliases { get; set; } = "";

    public string ImagePath { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    public string Rarity { get; set; } = "Обычный";

    public decimal? BlueTrophyWeightGrams { get; set; }

    public decimal? MinimumWeightGrams { get; set; }

    public decimal? QualifyingWeightGrams { get; set; }

    public decimal? ChatWeightGrams { get; set; }

    public decimal? MaximumWeightGrams { get; set; }

    public string WaterBodies { get; set; } = "";

    public string BiteActivity { get; set; } = "";

    public string Population { get; set; } = "";

    public string Layer { get; set; } = "";

    public string BestTime { get; set; } = "";

    public string TrophyType { get; set; } = "";

    public string Hook { get; set; } = "";

    public string Leader { get; set; } = "";

    public string Description { get; set; } = "";

    public string Tips { get; set; } = "";

    public string BaitHints { get; set; } = "";

    public decimal? CardPrice { get; set; }

    public List<FishRateBand> RateBands { get; set; } = [];

    [JsonIgnore]
    public string HabitatDisplay => string.IsNullOrWhiteSpace(Habitat)
        ? "—"
        : Habitat;

    [JsonIgnore]
    public string TrophyWeightDisplay => TrophyWeightGrams.HasValue
        ? WeightDisplayFormatter.Format(TrophyWeightGrams.Value / 1000m)
        : "—";

    [JsonIgnore]
    public string RarityDisplay => string.IsNullOrWhiteSpace(Rarity)
        ? "Обычный"
        : Rarity;

    [JsonIgnore]
    public string BlueTrophyWeightDisplay => FormatGrams(
        BlueTrophyWeightGrams);

    [JsonIgnore]
    public string MinimumWeightDisplay => FormatGrams(MinimumWeightGrams);

    [JsonIgnore]
    public string QualifyingWeightDisplay => FormatGrams(
        QualifyingWeightGrams);

    [JsonIgnore]
    public string ChatWeightDisplay => FormatGrams(ChatWeightGrams);

    [JsonIgnore]
    public string MaximumWeightDisplay => FormatGrams(MaximumWeightGrams);

    [JsonIgnore]
    public string WaterBodiesDisplay => string.IsNullOrWhiteSpace(WaterBodies)
        ? HabitatDisplay
        : WaterBodies;

    [JsonIgnore]
    public IReadOnlyList<string> WaterBodyNames =>
        SplitList(WaterBodies, HabitatDisplay);

    [JsonIgnore]
    public string WaterBodyCountDisplay =>
        $"ВОДОЁМЫ ({WaterBodyNames.Count})";

    [JsonIgnore]
    public string BiteActivityDisplay => ValueOrDash(BiteActivity);

    [JsonIgnore]
    public string PopulationDisplay => ValueOrDash(Population);

    [JsonIgnore]
    public string LayerDisplay => ValueOrDash(Layer);

    [JsonIgnore]
    public string BestTimeDisplay => ValueOrDash(BestTime);

    [JsonIgnore]
    public string TrophyTypeDisplay => ValueOrDash(TrophyType);

    [JsonIgnore]
    public string HookDisplay => ValueOrDash(Hook);

    [JsonIgnore]
    public string LeaderDisplay => ValueOrDash(Leader);

    [JsonIgnore]
    public string CardPriceDisplay => CardPrice.HasValue
        ? CardPrice.Value.ToString("0.##")
        : "—";

    [JsonIgnore]
    public string DescriptionDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Description))
            {
                return Description;
            }

            var family = string.IsNullOrWhiteSpace(Family)
                ? "указанному семейству"
                : $"семейству «{Family}»";
            var habitat = string.IsNullOrWhiteSpace(WaterBodies)
                ? HabitatDisplay.ToLowerInvariant()
                : WaterBodies;
            return $"{Name} относится к {family}. " +
                   $"Основные места обитания: {habitat}.";
        }
    }

    [JsonIgnore]
    public string BaitHintsDisplay => string.IsNullOrWhiteSpace(BaitHints)
        ? "Данные о наживках пока не заполнены."
        : BaitHints;

    [JsonIgnore]
    public IReadOnlyList<string> ReferenceBaitNames =>
        SplitList(BaitHints, "");

    [JsonIgnore]
    public string TipsDisplay => string.IsNullOrWhiteSpace(Tips)
        ? "Добавьте советы по ловле в редакторе каталога."
        : Tips;

    [JsonIgnore]
    public IReadOnlyList<FishRateBand> RateBandsDisplay =>
        RateBands.Count > 0
            ? RateBands
            : FishRateBand.Placeholders;

    private static string FormatGrams(decimal? grams)
    {
        return grams.HasValue
            ? WeightDisplayFormatter.Format(grams.Value / 1000m)
            : "—";
    }

    private static string ValueOrDash(string value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static IReadOnlyList<string> SplitList(
        string value,
        string fallback)
    {
        var values = value
            .Split(
                [';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();

        return values.Count > 0 || string.IsNullOrWhiteSpace(fallback)
            ? values
            : [fallback];
    }
}

public sealed class FishRateBand
{
    public string Name { get; set; } = "";

    public decimal? MinKilograms { get; set; }

    public decimal? MaxKilograms { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MinExperience { get; set; }

    public decimal? MaxExperience { get; set; }

    [JsonIgnore]
    public string WeightDisplay => FormatRange(
        MinKilograms,
        MaxKilograms,
        "0.###");

    [JsonIgnore]
    public string PriceDisplay => FormatRange(
        MinPrice,
        MaxPrice,
        "0.##");

    [JsonIgnore]
    public string ExperienceDisplay => FormatRange(
        MinExperience,
        MaxExperience,
        "0.##");

    public static IReadOnlyList<FishRateBand> Placeholders { get; } =
    [
        new() { Name = "Незачётная" },
        new() { Name = "Зачётная" },
        new() { Name = "Трофей" },
        new() { Name = "Синий трофей" }
    ];

    private static string FormatRange(
        decimal? min,
        decimal? max,
        string format)
    {
        if (!min.HasValue && !max.HasValue)
        {
            return "—";
        }

        if (min.HasValue && max.HasValue)
        {
            return $"{min.Value.ToString(format)} – " +
                   $"{max.Value.ToString(format)}";
        }

        return (min ?? max)!.Value.ToString(format);
    }
}
