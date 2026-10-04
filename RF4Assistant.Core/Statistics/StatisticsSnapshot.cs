namespace RF4AssistantPro.Statistics;

public sealed class StatisticsSnapshot
{
    public CatchStatistics Summary { get; init; } = new();

    public IReadOnlyList<WaterBodyRating> WaterBodies { get; init; } = [];

    public IReadOnlyList<FishChart> FishDistribution { get; init; } = [];

    public IReadOnlyList<BaitChart> BaitDistribution { get; init; } = [];

    public IReadOnlyList<PeriodAnalyticsRow> PeriodAnalytics { get; init; } = [];

    public IReadOnlyList<SessionAnalyticsRow> SessionAnalytics { get; init; } = [];

    public IReadOnlyList<AnalyticsRow> FishAnalytics { get; init; } = [];

    public IReadOnlyList<AnalyticsRow> BaitAnalytics { get; init; } = [];
}