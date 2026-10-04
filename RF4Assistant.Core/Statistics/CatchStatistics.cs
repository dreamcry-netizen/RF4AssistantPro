using RF4AssistantPro.Models;

namespace RF4AssistantPro.Statistics;

public sealed class CatchStatistics
{
    public int TotalCatches { get; init; }

    public decimal BestWeightKg { get; init; }

    public string BestFish { get; init; } = "";

    public string BestWaterBody { get; init; } = "";

    public decimal AverageWeightKg { get; init; }

    public string BestWeightDisplay =>
        WeightDisplayFormatter.Format(BestWeightKg);

    public string AverageWeightDisplay =>
        WeightDisplayFormatter.Format(AverageWeightKg);
}