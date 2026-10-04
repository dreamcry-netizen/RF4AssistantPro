using RF4AssistantPro.Models;

namespace RF4AssistantPro.Statistics;

public sealed class WaterBodyRating
{
    public int Rank { get; init; }

    public string Name { get; init; } = "";

    public int CatchCount { get; init; }

    public decimal AverageWeightKg { get; init; }

    public string AverageWeightDisplay =>
        WeightDisplayFormatter.Format(AverageWeightKg);

    public decimal BestWeightKg { get; init; }

    public string BestWeightDisplay =>
        WeightDisplayFormatter.Format(BestWeightKg);

    public int UniqueFishCount { get; init; }

    public DateTime? LastCatchAt { get; init; }

    public string LastCatchDisplay =>
        LastCatchAt.HasValue
            ? LastCatchAt.Value.ToString("dd.MM.yyyy HH:mm")
            : "Нет записей";
}