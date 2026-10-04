using System.IO;
using System.Text.Json;
using RF4AssistantPro.Models;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Storage;

public sealed class KeepnetRecordStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RF4AssistantPro",
        "keepnet.json");

    public List<KeepnetRecord> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var records = JsonSerializer.Deserialize<List<KeepnetRecord>>(
            File.ReadAllText(FilePath),
            Options) ?? [];
        var migrated = records
            .Select(record =>
            {
                var updated = record;
                if (updated.Id == Guid.Empty)
                {
                    updated = updated with { Id = Guid.NewGuid() };
                }

                if (string.IsNullOrWhiteSpace(updated.Source))
                {
                    updated = updated with { Source = RecordSource.Keepnet };
                }

                if (string.IsNullOrWhiteSpace(updated.ScreenshotHash))
                {
                    var hash = RecordIdentityService
                        .TryComputeScreenshotHash(updated.ScreenshotPath);
                    if (hash.Length > 0)
                    {
                        updated = updated with { ScreenshotHash = hash };
                    }
                }

                return updated;
            })
            .ToList();

        if (!records.SequenceEqual(migrated) ||
            MissingIdentityProperties(File.ReadAllText(FilePath)))
        {
            Save(migrated);
        }

        return migrated;
    }

    private static bool MissingIdentityProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .EnumerateArray()
            .Any(item =>
                !item.TryGetProperty("Id", out _) ||
                !item.TryGetProperty("Source", out _) ||
                !item.TryGetProperty("ScreenshotHash", out _));
    }

    public void Save(IEnumerable<KeepnetRecord> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = $"{FilePath}.tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(records.ToList(), Options));
        File.Move(temporary, FilePath, true);
    }
}