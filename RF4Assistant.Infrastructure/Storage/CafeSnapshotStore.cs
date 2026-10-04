using System.IO;
using System.Text.Json;
using RF4AssistantPro.Cafe;

namespace RF4AssistantPro.Storage;

public sealed class CafeSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CafeSnapshotStore(string? filePath = null)
    {
        FilePath = filePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RF4AssistantPro",
                "cafe-snapshots.json");
    }

    public string FilePath { get; }

    public IReadOnlyList<CafeSnapshot> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var json = File.ReadAllText(FilePath);
        var snapshots = JsonSerializer.Deserialize<List<CafeSnapshot>>(
            json,
            JsonOptions) ?? [];
        var migrated = snapshots
            .Select(snapshot =>
            {
                var changed = false;
                var offers = snapshot.Offers
                    .Select(offer =>
                    {
                        if (offer.Id != Guid.Empty)
                        {
                            return offer;
                        }

                        changed = true;
                        return CafeOfferCloner.WithId(offer, Guid.NewGuid());
                    })
                    .ToList();

                var matchedCounts = snapshot.OfferMatchedCounts
                    .Where(item => item.Key != Guid.Empty)
                    .ToDictionary(item => item.Key, item => item.Value);
                if (matchedCounts.Count != snapshot.OfferMatchedCounts.Count)
                {
                    changed = true;
                }

                return changed
                    ? new CafeSnapshot
                    {
                        CapturedAt = snapshot.CapturedAt,
                        FullImagePath = snapshot.FullImagePath,
                        ThumbnailPath = snapshot.ThumbnailPath,
                        Offers = offers,
                        OfferMatchedCounts = matchedCounts,
                        MatchedCatchCount = snapshot.MatchedCatchCount
                    }
                    : snapshot;
            })
            .ToList();

        if (!snapshots.SequenceEqual(migrated) ||
            MissingIdentityProperties(json))
        {
            Save(migrated);
        }

        return migrated;
    }

    private static bool MissingIdentityProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var snapshot in document.RootElement.EnumerateArray())
        {
            if (!snapshot.TryGetProperty("OfferMatchedCounts", out _))
            {
                return true;
            }

            if (!snapshot.TryGetProperty("Offers", out var offers))
            {
                continue;
            }

            if (offers.EnumerateArray().Any(
                    offer => !offer.TryGetProperty("Id", out _)))
            {
                return true;
            }
        }

        return false;
    }

    public void Save(IEnumerable<CafeSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{FilePath}.tmp";
        var json = JsonSerializer.Serialize(snapshots.Take(20).ToList(), JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, FilePath, true);
    }

}