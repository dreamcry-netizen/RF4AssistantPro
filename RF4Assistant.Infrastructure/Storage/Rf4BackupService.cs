using System.IO.Compression;
using System.IO;
using System.Text.Json;
using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Models;
using RF4AssistantPro.Statistics;

namespace RF4AssistantPro.Storage;

public sealed class Rf4BackupService
{
    private const int MaxArchiveEntries = 500;
    private const long MaxSingleEntryBytes = 50L * 1024 * 1024;
    private const long MaxTotalUncompressedBytes = 500L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void Export(
        string archivePath,
        IEnumerable<CatchRecord> catches,
        IEnumerable<CafeSnapshot> snapshots,
        IEnumerable<KeepnetRecord>? keepnet = null,
        IEnumerable<BaitCatalogItem>? baitCatalog = null,
        IEnumerable<UnrecognizedBait>? unrecognizedBaits = null,
        IEnumerable<FishingSession>? sessions = null,
        StatisticsSnapshot? statistics = null,
        IReadOnlyDictionary<string, string>? cafeOcrAliases = null,
        IReadOnlyDictionary<string, string>? keepnetOcrAliases = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(catches);
        ArgumentNullException.ThrowIfNull(snapshots);

        var assets = new List<AssetCopy>();
        var assetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var exportedCatches = catches
            .Select(record => record with
            {
                ScreenshotPath = AddAsset(
                    record.ScreenshotPath,
                    "assets/screenshots",
                    assets,
                    assetNames),
                BaitImagePath = AddAsset(
                    record.BaitImagePath,
                    "assets/baits",
                    assets,
                    assetNames)
            })
            .ToList();

        var exportedKeepnet = (keepnet ?? [])
            .Select(record => record with
            {
                ScreenshotPath = AddAsset(
                    record.ScreenshotPath,
                    "assets/screenshots",
                    assets,
                    assetNames)
            })
            .ToList();

        var exportedBaitCatalog = (baitCatalog ?? [])
            .Select(item => new BaitCatalogItem
            {
                Id = item.Id,
                Name = item.Name,
                Brand = item.Brand,
                Category = item.Category,
                Subcategory = item.Subcategory,
                Aliases = item.Aliases,
                ImagePath = AddAsset(
                    item.ImagePath,
                    "assets/baits",
                    assets,
                    assetNames),
                IsEnabled = item.IsEnabled
            })
            .ToList();

        var exportedUnrecognizedBaits = (unrecognizedBaits ?? [])
            .Select(item => new UnrecognizedBait
            {
                Id = item.Id,
                CapturedAt = item.CapturedAt,
                CandidateName = item.CandidateName,
                RawText = item.RawText,
                ScreenshotPath = AddAsset(
                    item.ScreenshotPath,
                    "assets/screenshots",
                    assets,
                    assetNames)
            })
            .ToList();

        var exportedSnapshots = snapshots
            .Select(snapshot => new CafeSnapshot
            {
                CapturedAt = snapshot.CapturedAt,
                FullImagePath = AddAsset(
                    snapshot.FullImagePath,
                    "assets/cafe",
                    assets,
                    assetNames),
                ThumbnailPath = AddAsset(
                    snapshot.ThumbnailPath,
                    "assets/cafe",
                    assets,
                    assetNames),
                Offers = snapshot.Offers.ToList(),
                OfferMatchedCounts =
                    snapshot.OfferMatchedCounts.ToDictionary(
                        item => item.Key,
                        item => item.Value),
                MatchedCatchCount = snapshot.MatchedCatchCount
            })
            .ToList();

        var directory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{archivePath}.tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        try
        {
            using (var archive = ZipFile.Open(
                       temporaryPath,
                       ZipArchiveMode.Create))
            {
                foreach (var asset in assets)
                {
                    archive.CreateEntryFromFile(
                        asset.SourcePath,
                        asset.EntryName);
                }

                WriteJson(archive, "catches.json", exportedCatches);
                WriteJson(archive, "cafe-snapshots.json", exportedSnapshots);
                WriteJson(archive, "keepnet.json", exportedKeepnet);
                WriteJson(
                    archive,
                    "bait-catalog.json",
                    exportedBaitCatalog);
                WriteJson(
                    archive,
                    "unrecognized-baits.json",
                    exportedUnrecognizedBaits);
                WriteJson(
                    archive,
                    "sessions.json",
                    (sessions ?? []).ToList());
                if (statistics is not null)
                {
                    WriteJson(archive, "analytics.json", statistics);
                }
                if (cafeOcrAliases is not null)
                {
                    WriteJson(
                        archive,
                        "cafe-ocr-aliases.json",
                        cafeOcrAliases);
                }
                if (keepnetOcrAliases is not null)
                {
                    WriteJson(
                        archive,
                        "keepnet-ocr-aliases.json",
                        keepnetOcrAliases);
                }
                WriteText(
                    archive,
                    "manifest.json",
                    "{\n  \"format\": \"RF4AssistantPro\",\n  \"version\": 2\n}\n");
            }

            File.Move(temporaryPath, archivePath, true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public BackupData Import(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        using var archive = ZipFile.OpenRead(archivePath);
        ValidateArchive(archive);
        ValidateManifest(archive);

        // Сначала читаем и проверяем JSON. Файлы извлекаются только после
        // успешной десериализации основных данных.
        var catches = ReadJson<List<CatchRecord>>(archive, "catches.json") ?? [];
        var snapshots = ReadJson<List<CafeSnapshot>>(
            archive,
            "cafe-snapshots.json") ?? [];
        var keepnet = ReadOptionalJson<List<KeepnetRecord>>(
            archive,
            "keepnet.json") ?? [];
        var baitCatalog = ReadOptionalJson<List<BaitCatalogItem>>(
            archive,
            "bait-catalog.json") ?? [];
        var unrecognizedBaits =
            ReadOptionalJson<List<UnrecognizedBait>>(
                archive,
                "unrecognized-baits.json") ?? [];
        var sessions = ReadOptionalJson<List<FishingSession>>(
            archive,
            "sessions.json") ?? [];
        var hasBaitCatalog = archive.GetEntry("bait-catalog.json") is not null;
        var hasUnrecognizedBaits =
            archive.GetEntry("unrecognized-baits.json") is not null;
        var cafeOcrAliases =
            ReadOptionalJson<Dictionary<string, string>>(
                archive,
                "cafe-ocr-aliases.json") ?? [];
        var hasCafeOcrAliases =
            archive.GetEntry("cafe-ocr-aliases.json") is not null;
        var keepnetOcrAliases =
            ReadOptionalJson<Dictionary<string, string>>(
                archive,
                "keepnet-ocr-aliases.json") ?? [];
        var hasKeepnetOcrAliases =
            archive.GetEntry("keepnet-ocr-aliases.json") is not null;

        var importRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Imported",
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));

        try
        {
            Directory.CreateDirectory(importRoot);
            foreach (var entry in archive.Entries.Where(item =>
                         item.FullName.StartsWith(
                             "assets/",
                             StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(item.Name)))
            {
                ExtractSafe(entry, importRoot);
            }

            var importedCatches = catches
                .Select(record => record with
                {
                    ScreenshotPath = ResolveAsset(
                        record.ScreenshotPath,
                        importRoot),
                    BaitImagePath = ResolveAsset(
                        record.BaitImagePath,
                        importRoot)
                })
                .ToList();

            var importedSnapshots = snapshots
                .Select(snapshot => new CafeSnapshot
                {
                    CapturedAt = snapshot.CapturedAt,
                    FullImagePath = ResolveAsset(
                        snapshot.FullImagePath,
                        importRoot),
                    ThumbnailPath = ResolveAsset(
                        snapshot.ThumbnailPath,
                        importRoot),
                    Offers = snapshot.Offers.ToList(),
                    OfferMatchedCounts =
                        snapshot.OfferMatchedCounts.ToDictionary(
                            item => item.Key,
                            item => item.Value),
                    MatchedCatchCount = snapshot.MatchedCatchCount
                })
                .ToList();

            var importedKeepnet = keepnet
                .Select(record => record with
                {
                    ScreenshotPath = ResolveAsset(
                        record.ScreenshotPath,
                        importRoot)
                })
                .ToList();

            var importedBaitCatalog = baitCatalog
                .Select(item => new BaitCatalogItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Brand = item.Brand,
                    Category = item.Category,
                    Subcategory = item.Subcategory,
                    Aliases = item.Aliases,
                    ImagePath = ResolveAsset(
                        item.ImagePath,
                        importRoot),
                    IsEnabled = item.IsEnabled
                })
                .ToList();

            var importedUnrecognizedBaits = unrecognizedBaits
                .Select(item => new UnrecognizedBait
                {
                    Id = item.Id,
                    CapturedAt = item.CapturedAt,
                    CandidateName = item.CandidateName,
                    RawText = item.RawText,
                    ScreenshotPath = ResolveAsset(
                        item.ScreenshotPath,
                        importRoot)
                })
                .ToList();

            return new BackupData(
                importedCatches,
                importedSnapshots,
                importedKeepnet,
                importedBaitCatalog,
                importedUnrecognizedBaits,
                sessions,
                importRoot,
                hasBaitCatalog,
                hasUnrecognizedBaits,
                cafeOcrAliases,
                hasCafeOcrAliases,
                keepnetOcrAliases,
                hasKeepnetOcrAliases);
        }
        catch
        {
            if (Directory.Exists(importRoot))
            {
                Directory.Delete(importRoot, true);
            }

            throw;
        }
    }

    public void CleanupFailedImport(BackupData backup)
    {
        if (string.IsNullOrWhiteSpace(backup.ImportDirectory))
        {
            return;
        }

        var importedRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Imported")) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(backup.ImportDirectory);
        if (candidate.StartsWith(importedRoot, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(candidate))
        {
            Directory.Delete(candidate, true);
        }
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxArchiveEntries)
        {
            throw new InvalidDataException(
                $"В архиве слишком много файлов: {archive.Entries.Count}.");
        }

        long totalSize = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxSingleEntryBytes)
            {
                throw new InvalidDataException(
                    $"Файл {entry.FullName} превышает допустимый размер.");
            }

            totalSize = checked(totalSize + entry.Length);
            if (totalSize > MaxTotalUncompressedBytes)
            {
                throw new InvalidDataException(
                    "Распакованный архив превышает допустимый размер.");
            }
        }
    }

    private static void ValidateManifest(ZipArchive archive)
    {
        var manifest = ReadJson<BackupManifest>(archive, "manifest.json")
            ?? throw new InvalidDataException("Манифест архива пуст.");

        if (!manifest.Format.Equals(
                "RF4AssistantPro",
                StringComparison.Ordinal) ||
            manifest.Version is not (1 or 2))
        {
            throw new InvalidDataException(
                "Неподдерживаемый формат архива RF4.");
        }
    }

    private static string AddAsset(
        string sourcePath,
        string folder,
        ICollection<AssetCopy> assets,
        IDictionary<string, string> assetNames)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return "";
        }

        var fullPath = Path.GetFullPath(sourcePath);
        if (assetNames.TryGetValue(fullPath, out var existingName))
        {
            return existingName;
        }

        var extension = Path.GetExtension(fullPath);
        var entryName = $"{folder}/{assets.Count + 1:0000}{extension}";
        assetNames[fullPath] = entryName;
        assets.Add(new AssetCopy(fullPath, entryName));
        return entryName;
    }

    private static void ExtractSafe(ZipArchiveEntry entry, string root)
    {
        var rootPath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
        if (!destinationPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Архив содержит недопустимый путь файла.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        entry.ExtractToFile(destinationPath, true);
    }

    private static string ResolveAsset(string path, string importRoot)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var relativePath = path.Replace('/', Path.DirectorySeparatorChar);
        var rootPath = Path.GetFullPath(importRoot) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(importRoot, relativePath));
        return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : "";
    }

    private static void WriteJson<T>(ZipArchive archive, string name, T value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static void WriteText(ZipArchive archive, string name, string value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(value);
    }

    private static T? ReadJson<T>(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidDataException($"В архиве отсутствует файл {name}.");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }

    private static T? ReadOptionalJson<T>(
        ZipArchive archive,
        string name)
    {
        var entry = archive.GetEntry(name);
        if (entry is null)
        {
            return default;
        }

        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }

    private sealed record AssetCopy(string SourcePath, string EntryName);

    private sealed class BackupManifest
    {
        public string Format { get; init; } = "";

        public int Version { get; init; }
    }
}

public sealed record BackupData(
    IReadOnlyList<CatchRecord> Catches,
    IReadOnlyList<CafeSnapshot> CafeSnapshots,
    IReadOnlyList<KeepnetRecord> Keepnet,
    IReadOnlyList<BaitCatalogItem> BaitCatalog,
    IReadOnlyList<UnrecognizedBait> UnrecognizedBaits,
    IReadOnlyList<FishingSession> Sessions,
    string ImportDirectory,
    bool HasBaitCatalog = false,
    bool HasUnrecognizedBaits = false,
    IReadOnlyDictionary<string, string>? CafeOcrAliases = null,
    bool HasCafeOcrAliases = false,
    IReadOnlyDictionary<string, string>? KeepnetOcrAliases = null,
    bool HasKeepnetOcrAliases = false);
