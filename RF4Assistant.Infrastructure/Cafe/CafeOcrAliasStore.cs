using System.IO;
using System.Text.Json;

namespace RF4AssistantPro.Cafe;

public sealed class CafeOcrAliasStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _sync = new();

    public CafeOcrAliasStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Cafe",
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
                    !string.IsNullOrWhiteSpace(item.Key) &&
                    !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(
                    item => Normalize(item.Key),
                    item => item.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Replace(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        lock (_sync)
        {
            var normalized = aliases
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Key) &&
                    !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(
                    item => Normalize(item.Key),
                    item => item.Value.Trim(),
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

    public int LearnFromOffers(IEnumerable<CafeOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        lock (_sync)
        {
            var aliases = Load().ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);
            var changed = 0;
            foreach (var offer in offers)
            {
                var raw = Normalize(offer.RawFishName);
                var confirmed = offer.FishName.Trim();
                if (raw.Length < 2 ||
                    confirmed.Length < 2 ||
                    raw.Equals(confirmed, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!aliases.TryGetValue(raw, out var existing) ||
                    !existing.Equals(confirmed, StringComparison.Ordinal))
                {
                    aliases[raw] = confirmed;
                    changed++;
                }
            }

            if (changed > 0)
            {
                Save(aliases);
            }

            return changed;
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
