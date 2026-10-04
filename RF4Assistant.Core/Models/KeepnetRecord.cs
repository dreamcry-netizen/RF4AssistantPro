using System.Text.Json.Serialization;

namespace RF4AssistantPro.Models;

public sealed record KeepnetRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid? FishingSessionId { get; init; }

    public DateTime RecordedAt { get; init; }

    public string Source { get; init; } = RecordSource.Keepnet;

    public string FishName { get; init; } = "";

    public string WaterBodyName { get; init; } = "";

    public decimal WeightKg { get; init; }

    public bool IsCafeMatch { get; init; }

    public bool NeedsReview { get; init; }

    public string ScreenshotPath { get; init; } = "";

    public string ScreenshotHash { get; init; } = "";

    public string RawOcrText { get; init; } = "";

    public Guid? CafeOfferId { get; init; }

    public Guid? RelatedCatchId { get; init; }

    [JsonIgnore]
    public string FishImagePath { get; init; } = "";

    [JsonIgnore]
    public string RecordedAtDisplay => RecordedAt.ToString("dd.MM HH:mm");

    [JsonIgnore]
    public string WeightDisplay =>
        NeedsReview
            ? "—"
            : WeightDisplayFormatter.Format(WeightKg);

    [JsonIgnore]
    public string CafeMark =>
        NeedsReview
            ? "Требует проверки"
            : IsCafeMatch
                ? "Кафе"
                : "";
}