using System.Globalization;
using RF4AssistantPro.Models;
using RF4AssistantPro.Statistics;

namespace RF4AssistantPro.Services;

public sealed class StatisticsService
{
    public StatisticsSnapshot GetStatistics(
        IEnumerable<CatchRecord> catches,
        IEnumerable<FishingSession>? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(catches);

        var records = catches
            .Where(record => record.WeightKg >= 0)
            .ToList();

        if (records.Count == 0)
        {
            return new StatisticsSnapshot
            {
                SessionAnalytics = BuildSessionAnalytics(
                    Array.Empty<CatchRecord>(),
                    sessions ?? Array.Empty<FishingSession>())
            };
        }

        var weightedRecords = records
            .Where(record => record.WeightKg > 0m)
            .ToList();
        var bestCatch = weightedRecords
            .MaxBy(record => record.WeightKg);

        var waterBodies = records
            .GroupBy(record => record.WaterBodyName)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Average(record => record.WeightKg))
            .Select((group, index) => new WaterBodyRating
            {
                Rank = index + 1,
                Name = group.Key,
                CatchCount = group.Count(),
                AverageWeightKg = AverageKnownWeight(group),
                BestWeightKg = group
                    .Where(record => record.WeightKg > 0m)
                    .Select(record => record.WeightKg)
                    .DefaultIfEmpty(0m)
                    .Max(),
                UniqueFishCount = group
                    .Select(record => record.FishName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                LastCatchAt = group.Max(record => record.CaughtAt)
            })
            .ToList();

        var fishDistribution = BuildDistribution<FishChart>(
            records,
            record => record.FishName,
            (label, count, share) => new FishChart
            {
                Label = label,
                Value = count,
                SharePercent = share
            });

        var baitDistribution = BuildDistribution<BaitChart>(
            records,
            record => record.BaitName,
            (label, count, share) => new BaitChart
            {
                Label = label,
                Value = count,
                SharePercent = share
            });

        return new StatisticsSnapshot
        {
            Summary = new CatchStatistics
            {
                TotalCatches = records.Count,
                BestFish = bestCatch?.FishName ?? "",
                BestWeightKg = bestCatch?.WeightKg ?? 0m,
                BestWaterBody = bestCatch?.WaterBodyName ?? "",
                AverageWeightKg = weightedRecords.Count == 0
                    ? 0m
                    : weightedRecords.Average(
                        record => record.WeightKg)
            },
            WaterBodies = waterBodies,
            FishDistribution = fishDistribution,
            BaitDistribution = baitDistribution,
            PeriodAnalytics = BuildPeriodAnalytics(records),
            SessionAnalytics = BuildSessionAnalytics(records, sessions ?? []),
            FishAnalytics = BuildAnalyticsRows(records, record => record.FishName),
            BaitAnalytics = BuildAnalyticsRows(records, record => record.BaitName)
        };
    }

    private static List<PeriodAnalyticsRow> BuildPeriodAnalytics(
        IReadOnlyCollection<CatchRecord> records)
    {
        return records
            .GroupBy(record => record.CaughtAt.Date)
            .OrderByDescending(group => group.Key)
            .Select(group => BuildAnalyticsRow(
                group,
                group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (label, count, total, average, best, cafe) =>
                    new PeriodAnalyticsRow
                    {
                        PeriodStart = group.Key,
                        Label = label,
                        CatchCount = count,
                        TotalWeightKg = total,
                        AverageWeightKg = average,
                        BestWeightKg = best,
                        CafeMatchCount = cafe
                    }))
            .ToList();
    }

    private static List<AnalyticsRow> BuildAnalyticsRows(
        IReadOnlyCollection<CatchRecord> records,
        Func<CatchRecord, string> keySelector)
    {
        return records
            .GroupBy(keySelector)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => BuildAnalyticsRow(
                group,
                group.Key,
                (label, count, total, average, best, cafe) =>
                    new AnalyticsRow
                    {
                        Label = label,
                        CatchCount = count,
                        TotalWeightKg = total,
                        AverageWeightKg = average,
                        BestWeightKg = best,
                        CafeMatchCount = cafe
                    }))
            .ToList();
    }

    private static List<SessionAnalyticsRow> BuildSessionAnalytics(
        IReadOnlyCollection<CatchRecord> records,
        IEnumerable<FishingSession> sessions)
    {
        var sessionMap = sessions.ToDictionary(item => item.Id);
        var groups = records
            .GroupBy(record => record.FishingSessionId ?? Guid.Empty)
            .ToDictionary(group => group.Key);

        var result = groups
            .Select(pair =>
            {
                sessionMap.TryGetValue(pair.Key, out var session);
                if (pair.Key == Guid.Empty)
                {
                    session = null;
                }
                var label = session is null
                    ? "Без сессии"
                    : $"{session.StartedAt:dd.MM.yyyy HH:mm} · " +
                      $"{DisplayOrDash(session.WaterBodyName)}";
                return BuildAnalyticsRow(
                    pair.Value,
                    label,
                    (name, count, total, average, best, cafe) =>
                        new SessionAnalyticsRow
                        {
                            SessionId = pair.Key == Guid.Empty
                                ? null
                                : pair.Key,
                            StartedAt = session?.StartedAt,
                            EndedAt = session?.EndedAt,
                            WaterBodyName = session?.WaterBodyName ??
                                pair.Value.First().WaterBodyName,
                            BaitName = session?.BaitName ??
                                pair.Value.First().BaitName,
                            SessionLabel = name,
                            Label = name,
                            CatchCount = count,
                            TotalWeightKg = total,
                            AverageWeightKg = average,
                            BestWeightKg = best,
                            CafeMatchCount = cafe
                        });
            })
            .ToList();

        // Пустые сессии тоже полезны в аналитике: они показывают начатую
        // сессию, в которой пока не было уловов.
        result.AddRange(
            sessions
                .Where(session => !groups.ContainsKey(session.Id))
                .Select(session => new SessionAnalyticsRow
                {
                    SessionId = session.Id,
                    StartedAt = session.StartedAt,
                    EndedAt = session.EndedAt,
                    WaterBodyName = session.WaterBodyName,
                    BaitName = session.BaitName,
                    SessionLabel =
                        $"{session.StartedAt:dd.MM.yyyy HH:mm} · " +
                        $"{DisplayOrDash(session.WaterBodyName)}",
                    Label = "Пустая сессия"
                }));

        return result
            .OrderByDescending(item => item.StartedAt ?? DateTime.MinValue)
            .ToList();
    }

    private static TRow BuildAnalyticsRow<TRow>(
        IEnumerable<CatchRecord> records,
        string label,
        Func<string, int, decimal, decimal, decimal, int, TRow> factory)
    {
        var items = records.ToList();
        var known = items.Where(item => item.WeightKg > 0m).ToList();
        return factory(
            label,
            items.Count,
            known.Sum(item => item.WeightKg),
            known.Count == 0 ? 0m : known.Average(item => item.WeightKg),
            known.Count == 0 ? 0m : known.Max(item => item.WeightKg),
            items.Count(item => item.IsCafeMatch));
    }

    private static string DisplayOrDash(string value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static decimal AverageKnownWeight(
        IEnumerable<CatchRecord> records)
    {
        var known = records
            .Where(record => record.WeightKg > 0m)
            .ToList();
        return known.Count == 0
            ? 0m
            : known.Average(record => record.WeightKg);
    }

    public StatisticsSnapshot GetDemoStatistics()
    {
        return GetStatistics(GetDemoCatches());
    }

    public IReadOnlyList<CatchRecord> GetDemoCatches()
    {
        return
        [
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 28),
                FishName = "Щука",
                WaterBodyName = "Яр",
                BaitName = "Воблер",
                WeightKg = 4.80m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 28),
                FishName = "Окунь",
                WaterBodyName = "Яр",
                BaitName = "Джиг",
                WeightKg = 1.20m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 29),
                FishName = "Лещ",
                WaterBodyName = "Озеро Медвежье",
                BaitName = "Червь",
                WeightKg = 2.10m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 29),
                FishName = "Щука",
                WaterBodyName = "Яр",
                BaitName = "Воблер",
                WeightKg = 3.40m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 30),
                FishName = "Судак",
                WaterBodyName = "Вьюн",
                BaitName = "Джиг",
                WeightKg = 3.10m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 9, 30),
                FishName = "Окунь",
                WaterBodyName = "Вьюн",
                BaitName = "Джиг",
                WeightKg = 0.90m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 10, 1),
                FishName = "Щука",
                WaterBodyName = "Озеро Медвежье",
                BaitName = "Воблер",
                WeightKg = 2.70m
            },
            new CatchRecord
            {
                CaughtAt = new DateTime(2026, 10, 1),
                FishName = "Лещ",
                WaterBodyName = "Озеро Медвежье",
                BaitName = "Червь",
                WeightKg = 1.80m
            }
        ];
    }

    private static List<TChart> BuildDistribution<TChart>(
        IReadOnlyCollection<CatchRecord> records,
        Func<CatchRecord, string> keySelector,
        Func<string, int, double, TChart> factory)
    {
        return records
            .GroupBy(keySelector)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => factory(
                group.Key,
                group.Count(),
                Math.Round(group.Count() * 100d / records.Count, 1)))
            .ToList();
    }
}