namespace RF4AssistantPro.Ocr;

public sealed record RecognizedKeepnetFish(
    string FishName,
    decimal WeightKg,
    string RawText);