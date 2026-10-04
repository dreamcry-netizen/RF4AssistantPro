namespace RF4AssistantPro.Baits;

public sealed class UnrecognizedBait
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CapturedAt { get; set; } = DateTime.Now;

    public string CandidateName { get; set; } = "";

    public string RawText { get; set; } = "";

    public string ScreenshotPath { get; set; } = "";

    public string CapturedAtDisplay => CapturedAt.ToString("dd.MM.yyyy HH:mm:ss");
}