using System.Text.Json.Serialization;

namespace RF4AssistantPro.Models;

public sealed record CatchRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid? FishingSessionId { get; init; }

    public DateTime CaughtAt { get; init; }

    public string Source { get; init; } = RecordSource.Space;

    public string FishName { get; init; } = "";

    public string WaterBodyName { get; init; } = "";

    public string BaitName { get; init; } = "";

    public string BaitImagePath { get; init; } = "";

    public decimal WeightKg { get; init; }

    public decimal? LengthCm { get; init; }

    public string Quality { get; init; } = "";

    public string ScreenshotPath { get; init; } = "";

    public string ScreenshotHash { get; init; } = "";

    public Guid? CafeOfferId { get; init; }

    public Guid? RelatedKeepnetId { get; init; }

    public bool IsCafeMatch { get; init; }

    public bool NeedsReview { get; init; }

    [JsonIgnore]
    public string FishImagePath { get; init; } = "";

    [JsonIgnore]
    public string CafeMark => IsCafeMatch ? "Кафе" : "";

    [JsonIgnore]
    public string WeightDisplay =>
        NeedsReview
            ? "—"
            : WeightDisplayFormatter.Format(WeightKg);
}