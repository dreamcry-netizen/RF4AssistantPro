using RF4AssistantPro.Models;

namespace RF4AssistantPro.Statistics;

public class AnalyticsRow
{
    public string Label { get; init; } = "";

    public string FishImagePath { get; set; } = "";

    public int CatchCount { get; init; }

    public decimal TotalWeightKg { get; init; }

    public decimal AverageWeightKg { get; init; }

    public decimal BestWeightKg { get; init; }

    public int CafeMatchCount { get; init; }

    public string TotalWeightDisplay =>
        WeightDisplayFormatter.Format(TotalWeightKg);

    public string AverageWeightDisplay =>
        WeightDisplayFormatter.Format(AverageWeightKg);

    public string BestWeightDisplay =>
        WeightDisplayFormatter.Format(BestWeightKg);
}

public sealed class PeriodAnalyticsRow : AnalyticsRow
{
    public DateTime PeriodStart { get; init; }

    public string PeriodLabel => PeriodStart.ToString("dd.MM.yyyy");
}

public sealed class SessionAnalyticsRow : AnalyticsRow
{
    public Guid? SessionId { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? EndedAt { get; init; }

    public string WaterBodyName { get; init; } = "";

    public string BaitName { get; init; } = "";

    public string SessionLabel { get; init; } = "";

    public string StartedAtDisplay =>
        StartedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Без сессии";

    public string EndedAtDisplay =>
        EndedAt?.ToString("dd.MM.yyyy HH:mm") ?? "—";
}