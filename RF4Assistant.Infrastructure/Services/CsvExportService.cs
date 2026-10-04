using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Models;
using RF4AssistantPro.Statistics;

namespace RF4AssistantPro.Services;

public static class CsvExportService
{
    public static string Create(
        IEnumerable<CatchRecord> catches,
        IEnumerable<KeepnetRecord> keepnet,
        IEnumerable<CafeSnapshot> cafeSnapshots,
        IEnumerable<FishingSession>? sessions = null,
        StatisticsSnapshot? statistics = null)
    {
        var outputDirectory = Path.Combine(
            PortableDataPaths.BaseDirectory,
            "Exports");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(
            outputDirectory,
            $"RF4AssistantPro_export_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        using var archive = ZipFile.Open(
            outputPath,
            ZipArchiveMode.Create);
        WriteEntry(
            archive,
            "Catches.csv",
            BuildCatches(catches));
        WriteEntry(
            archive,
            "Keepnet.csv",
            BuildKeepnet(keepnet));
        WriteEntry(
            archive,
            "Cafe.csv",
            BuildCafe(cafeSnapshots));
        WriteEntry(
            archive,
            "Sessions.csv",
            BuildSessions(sessions ?? []));
        if (statistics is not null)
        {
            WriteEntry(
                archive,
                "Summary.csv",
                BuildSummary(statistics));
            WriteEntry(
                archive,
                "AnalyticsByPeriod.csv",
                BuildAnalytics(statistics.PeriodAnalytics, "Период"));
            WriteEntry(
                archive,
                "AnalyticsBySession.csv",
                BuildSessionsAnalytics(statistics.SessionAnalytics));
            WriteEntry(
                archive,
                "AnalyticsByWaterBody.csv",
                BuildWaterBodies(statistics.WaterBodies));
            WriteEntry(
                archive,
                "AnalyticsByFish.csv",
                BuildAnalytics(statistics.FishAnalytics, "Рыба"));
            WriteEntry(
                archive,
                "AnalyticsByBait.csv",
                BuildAnalytics(statistics.BaitAnalytics, "Наживка"));
            WriteJsonEntry(archive, "Summary.json", statistics);
            WriteEntry(
                archive,
                "README.txt",
                [
                    "RF4 Assistant Pro — единый сводный экспорт",
                    "CSV-файлы содержат исходные записи и агрегаты.",
                    "Summary.json содержит ту же сводку в формате JSON."
                ]);
        }
        return outputPath;
    }

    private static IEnumerable<string> BuildSummary(
        StatisticsSnapshot snapshot)
    {
        yield return string.Join(
            ';',
            "Показатель",
            "Значение");
        yield return Join("Всего уловов", snapshot.Summary.TotalCatches.ToString(
            CultureInfo.InvariantCulture));
        yield return Join("Лучший улов", snapshot.Summary.BestFish);
        yield return Join("Лучший вес, г",
            FormatNumber(snapshot.Summary.BestWeightKg * 1000m));
        yield return Join("Лучший водоём", snapshot.Summary.BestWaterBody);
        yield return Join("Средний вес, г",
            FormatNumber(snapshot.Summary.AverageWeightKg * 1000m));
        yield return Join("Периодов", snapshot.PeriodAnalytics.Count.ToString(
            CultureInfo.InvariantCulture));
        yield return Join("Сессий", snapshot.SessionAnalytics.Count.ToString(
            CultureInfo.InvariantCulture));
    }

    private static IEnumerable<string> BuildAnalytics(
        IEnumerable<AnalyticsRow> rows,
        string firstHeader)
    {
        yield return string.Join(
            ';',
            firstHeader,
            "Уловов",
            "Общий вес, г",
            "Средний вес, г",
            "Лучший вес, г",
            "Кафе");
        foreach (var row in rows)
        {
            yield return Join(
                row.Label,
                row.CatchCount.ToString(CultureInfo.InvariantCulture),
                FormatNumber(row.TotalWeightKg * 1000m),
                FormatNumber(row.AverageWeightKg * 1000m),
                FormatNumber(row.BestWeightKg * 1000m),
                row.CafeMatchCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static IEnumerable<string> BuildSessionsAnalytics(
        IEnumerable<SessionAnalyticsRow> rows)
    {
        yield return string.Join(
            ';',
            "Сессия",
            "Начало",
            "Окончание",
            "Водоём",
            "Наживка",
            "Уловов",
            "Общий вес, г",
            "Средний вес, г",
            "Лучший вес, г",
            "Кафе");
        foreach (var row in rows)
        {
            yield return Join(
                row.SessionLabel,
                row.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                row.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                row.WaterBodyName,
                row.BaitName,
                row.CatchCount.ToString(CultureInfo.InvariantCulture),
                FormatNumber(row.TotalWeightKg * 1000m),
                FormatNumber(row.AverageWeightKg * 1000m),
                FormatNumber(row.BestWeightKg * 1000m),
                row.CafeMatchCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static IEnumerable<string> BuildWaterBodies(
        IEnumerable<WaterBodyRating> rows)
    {
        yield return string.Join(
            ';',
            "Водоём",
            "Уловов",
            "Средний вес, г",
            "Лучший вес, г",
            "Видов рыб",
            "Последний улов");
        foreach (var row in rows)
        {
            yield return Join(
                row.Name,
                row.CatchCount.ToString(CultureInfo.InvariantCulture),
                FormatNumber(row.AverageWeightKg * 1000m),
                FormatNumber(row.BestWeightKg * 1000m),
                row.UniqueFishCount.ToString(CultureInfo.InvariantCulture),
                row.LastCatchAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
        }
    }

    private static IEnumerable<string> BuildCatches(
        IEnumerable<CatchRecord> catches)
    {
        yield return string.Join(
            ';',
            "Время",
            "Рыба",
            "Водоём",
            "Наживка",
            "Вес, г",
            "Длина, см",
            "Статус",
            "Кафе",
            "ID сессии");
        foreach (var item in catches.OrderBy(item => item.CaughtAt))
        {
            yield return Join(
                item.CaughtAt.ToString("yyyy-MM-dd HH:mm:ss"),
                item.FishName,
                item.WaterBodyName,
                item.BaitName,
                FormatNumber(item.WeightKg * 1000m),
                item.LengthCm.HasValue
                    ? FormatNumber(item.LengthCm.Value)
                    : "",
                item.Quality,
                item.IsCafeMatch ? "Да" : "Нет",
                item.FishingSessionId?.ToString() ?? "");
        }
    }

    private static IEnumerable<string> BuildKeepnet(
        IEnumerable<KeepnetRecord> keepnet)
    {
        yield return string.Join(
            ';',
            "Время",
            "Рыба",
            "Вес, г",
            "Кафе",
            "Требует проверки",
            "ID сессии");
        foreach (var item in keepnet.OrderBy(item => item.RecordedAt))
        {
            yield return Join(
                item.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                item.FishName,
                FormatNumber(item.WeightKg * 1000m),
                item.IsCafeMatch ? "Да" : "Нет",
                item.NeedsReview ? "Да" : "Нет",
                item.FishingSessionId?.ToString() ?? "");
        }
    }

    private static IEnumerable<string> BuildCafe(
        IEnumerable<CafeSnapshot> snapshots)
    {
        yield return string.Join(
            ';',
            "Время снимка",
            "Рыба",
            "Водоём",
            "Количество",
            "Масса от, г",
            "Единица OCR",
            "Источник веса",
            "Исходный текст веса",
            "Цена");
        foreach (var snapshot in snapshots
                     .OrderBy(item => item.CapturedAt))
        {
            foreach (var offer in snapshot.Offers)
            {
                yield return Join(
                    snapshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    offer.FishName,
                    offer.WaterBodyName,
                    offer.Quantity.ToString(CultureInfo.InvariantCulture),
                    offer.MinimumWeightGrams.HasValue
                        ? FormatNumber(offer.MinimumWeightGrams.Value)
                        : "",
                    offer.MinimumWeightUnit,
                    offer.WeightSource,
                    offer.RawWeightText,
                    offer.Price.HasValue
                        ? FormatNumber(offer.Price.Value)
                        : "");
            }
        }
    }

    private static IEnumerable<string> BuildSessions(
        IEnumerable<FishingSession> sessions)
    {
        yield return string.Join(
            ';',
            "ID",
            "Начало",
            "Окончание",
            "Водоём",
            "Наживка",
            "Заметки");
        foreach (var session in sessions.OrderBy(item => item.StartedAt))
        {
            yield return Join(
                session.Id.ToString(),
                session.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                session.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                session.WaterBodyName,
                session.BaitName,
                session.Notes);
        }
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        IEnumerable<string> lines)
    {
        var entry = archive.CreateEntry(
            name,
            CompressionLevel.Optimal);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(true));
        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }
    }

    private static void WriteJsonEntry(
        ZipArchive archive,
        string name,
        object value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(true));
        writer.Write(JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Join(params string[] values)
    {
        return string.Join(';', values.Select(Escape));
    }

    private static string Escape(string value)
    {
        if (!value.Contains(';') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatNumber(decimal value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}