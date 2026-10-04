namespace RF4AssistantPro.Models;

public sealed record FishingSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime StartedAt { get; init; }

    public DateTime? EndedAt { get; init; }

    public string WaterBodyName { get; init; } = "";

    public string BaitName { get; init; } = "";

    public string Notes { get; init; } = "";

    public bool IsActive => !EndedAt.HasValue;
}
