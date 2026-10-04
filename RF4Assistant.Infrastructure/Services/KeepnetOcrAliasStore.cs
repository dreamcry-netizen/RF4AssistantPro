using System.IO;
using System.Text.Json;

namespace RF4AssistantPro.Services;

public sealed class KeepnetOcrAliasStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _sync = new();

    public KeepnetOcrAliasStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Keepnet",
            "ocr-name-aliases.json");
    }

    public string FilePath => _path;

    public IReadOnlyDictionary<string, string> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_path))
            {
                return new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            }

            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_path),
                JsonOptions) ?? [];
            return stored
                .Where(item =>
                    Normalize(item.Key).Length >= 2 &&
                    Normalize(item.Value).Length >= 2)
                .ToDictionary(
                    item => Normalize(item.Key),
                    item => Normalize(item.Value),
                    StringComparer.OrdinalIgnoreCase);
        }
    }

    public string Resolve(string recognizedName)
    {
        var normalized = Normalize(recognizedName);
        lock (_sync)
        {
            var aliases = Load();
            return aliases.TryGetValue(normalized, out var corrected)
                ? corrected
                : normalized;
        }
    }

    public bool Learn(string recognizedName, string confirmedName)
    {
        var raw = Normalize(recognizedName);
        var confirmed = Normalize(confirmedName);
        if (raw.Length < 2 ||
            confirmed.Length < 2 ||
            raw.Equals("Не распознана", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals(confirmed, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (_sync)
        {
            var aliases = Load().ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);
            if (aliases.TryGetValue(raw, out var existing) &&
                existing.Equals(confirmed, StringComparison.Ordinal))
            {
                return false;
            }

            aliases[raw] = confirmed;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = $"{_path}.tmp";
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(aliases, JsonOptions));
            File.Move(temporary, _path, true);
            return true;
        }
    }

    public void Replace(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        lock (_sync)
        {
            var normalized = aliases
                .Where(item =>
                    Normalize(item.Key).Length >= 2 &&
                    Normalize(item.Value).Length >= 2 &&
                    !Normalize(item.Key).Equals(
                        "Не распознана",
                        StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    item => Normalize(item.Key),
                    item => Normalize(item.Value),
                    StringComparer.OrdinalIgnoreCase);
            if (normalized.Count == 0)
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
                return;
            }

            Save(normalized);
        }
    }

    private void Save(IReadOnlyDictionary<string, string> aliases)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = $"{_path}.tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(aliases, JsonOptions));
        File.Move(temporary, _path, true);
    }

    private static string Normalize(string value) => string.Join(
        " ",
        (value ?? "").Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).Trim();
}
