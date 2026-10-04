using System.IO;
using System.Text.Json;
using RF4AssistantPro.Models;

namespace RF4AssistantPro.Storage;

public sealed class FishingSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public FishingSessionStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "fishing-sessions.json");
    }

    public string FilePath { get; }

    public IReadOnlyList<FishingSession> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<FishingSession>>(
                   File.ReadAllText(FilePath),
                   JsonOptions) ?? [];
    }

    public void Save(IEnumerable<FishingSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{FilePath}.tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(
                sessions.OrderByDescending(item => item.StartedAt)
                    .Take(500)
                    .ToList(),
                JsonOptions));
        File.Move(temporaryPath, FilePath, true);
    }
}
