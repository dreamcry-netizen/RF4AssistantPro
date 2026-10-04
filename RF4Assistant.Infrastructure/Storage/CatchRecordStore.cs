using System.IO;
using System.Text.Json;
using RF4AssistantPro.Models;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Storage;

public sealed class CatchRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CatchRecordStore(string? filePath = null)
    {
        FilePath = filePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RF4AssistantPro",
                "catches.json");
    }

    public string FilePath { get; }

    public IReadOnlyList<CatchRecord> Load()
    {
        return Load(FilePath);
    }

    public IReadOnlyList<CatchRecord> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var json = File.ReadAllText(path);
        var records = JsonSerializer.Deserialize<List<CatchRecord>>(
            json,
            JsonOptions) ?? [];
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
                    updated = updated with { Source = RecordSource.Imported };
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
            MissingIdentityProperties(json))
        {
            Save(migrated, path);
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

    public void Save(IEnumerable<CatchRecord> catches)
    {
        Save(catches, FilePath);
    }

    public void Save(IEnumerable<CatchRecord> catches, string path)
    {
        ArgumentNullException.ThrowIfNull(catches);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(catches.ToList(), JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, true);
    }
}