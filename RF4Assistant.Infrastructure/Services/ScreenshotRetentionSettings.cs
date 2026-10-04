using System.IO;
using System.Text.Json;

namespace RF4AssistantPro.Services;

public sealed class ScreenshotRetentionSettings
{
    public int MaxAgeDays { get; init; } = 30;

    public long MaxTotalMegabytes { get; init; } = 1024;

    public bool AutoCleanupEnabled { get; init; }

    public ScreenshotRetentionSettings Normalize()
    {
        return new ScreenshotRetentionSettings
        {
            MaxAgeDays = Math.Clamp(MaxAgeDays, 0, 3650),
            MaxTotalMegabytes = Math.Clamp(MaxTotalMegabytes, 0, 1_048_576),
            AutoCleanupEnabled = AutoCleanupEnabled
        };
    }
}

public sealed class ScreenshotRetentionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;

    public ScreenshotRetentionSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? PortableDataPaths.ScreenshotSettingsPath;
    }

    public ScreenshotRetentionSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new ScreenshotRetentionSettings();
        }

        try
        {
            return (JsonSerializer.Deserialize<ScreenshotRetentionSettings>(
                        File.ReadAllText(_filePath),
                        JsonOptions) ?? new ScreenshotRetentionSettings())
                .Normalize();
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось загрузить настройки хранения снимков: " +
                exception.Message);
            return new ScreenshotRetentionSettings();
        }
    }

    public void Save(ScreenshotRetentionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_filePath}.tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(temporaryPath, _filePath, true);
    }
}
