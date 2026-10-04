using System.IO;

namespace RF4AssistantPro.Services;

public sealed record ScreenshotStorageFile(
    string Path,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsProtected);

public sealed record ScreenshotStorageSummary(
    int FileCount,
    long TotalBytes,
    int ProtectedFileCount,
    long ProtectedBytes)
{
    public int UnprotectedFileCount => FileCount - ProtectedFileCount;

    public long UnprotectedBytes => TotalBytes - ProtectedBytes;

    public string DisplayText =>
        $"PNG: {FileCount}; объём: {FormatBytes(TotalBytes)}; " +
        $"защищено ссылками: {ProtectedFileCount}; " +
        $"можно удалить: {UnprotectedFileCount}";

    public static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024L * 1024L =>
                $"{bytes / 1024d / 1024d / 1024d:0.##} ГБ",
            >= 1024L * 1024L =>
                $"{bytes / 1024d / 1024d:0.##} МБ",
            >= 1024L =>
                $"{bytes / 1024d:0.##} КБ",
            _ => $"{bytes} Б"
        };
    }
}

public sealed record ScreenshotCleanupResult(
    int ScannedFileCount,
    int DeletedFileCount,
    int ProtectedFileCount,
    long FreedBytes,
    long RemainingBytes,
    IReadOnlyList<string> Errors)
{
    public string DisplayText =>
        $"Удалено: {DeletedFileCount}; освобождено: " +
        $"{ScreenshotStorageSummary.FormatBytes(FreedBytes)}; " +
        $"осталось: {ScreenshotStorageSummary.FormatBytes(RemainingBytes)}" +
        (Errors.Count == 0 ? "" : $"; ошибок: {Errors.Count}");
}

public sealed class ScreenshotStorageService
{
    private readonly string _rootDirectory;

    public ScreenshotStorageService(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? PortableDataPaths.ScreenshotsDirectory;
    }

    public ScreenshotStorageSummary GetSummary(
        IEnumerable<string>? protectedPaths = null)
    {
        var protectedSet = NormalizePaths(protectedPaths);
        var files = EnumerateFiles();
        return new ScreenshotStorageSummary(
            files.Count,
            files.Sum(file => file.SizeBytes),
            files.Count(file => protectedSet.Contains(file.Path)),
            files.Where(file => protectedSet.Contains(file.Path))
                .Sum(file => file.SizeBytes));
    }

    public ScreenshotCleanupResult Cleanup(
        ScreenshotRetentionSettings settings,
        IEnumerable<string>? protectedPaths = null,
        DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalizedSettings = settings.Normalize();
        var protectedSet = NormalizePaths(protectedPaths);
        var files = EnumerateFiles();
        var candidates = files
            .Where(file => !protectedSet.Contains(file.Path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var deletePaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var currentBytes = files.Sum(file => file.SizeBytes);
        var cutoff = (nowUtc ?? DateTime.UtcNow)
            .ToUniversalTime()
            .AddDays(-normalizedSettings.MaxAgeDays);

        if (normalizedSettings.MaxAgeDays > 0)
        {
            foreach (var file in candidates.Where(file =>
                         file.LastWriteTimeUtc < cutoff))
            {
                deletePaths.Add(file.Path);
                currentBytes -= file.SizeBytes;
            }
        }

        if (normalizedSettings.MaxTotalMegabytes > 0)
        {
            var maximumBytes = checked(
                normalizedSettings.MaxTotalMegabytes * 1024L * 1024L);
            foreach (var file in candidates)
            {
                if (currentBytes <= maximumBytes ||
                    deletePaths.Contains(file.Path))
                {
                    continue;
                }

                deletePaths.Add(file.Path);
                currentBytes -= file.SizeBytes;
            }
        }

        var errors = new List<string>();
        var deleted = 0;
        long freedBytes = 0;
        foreach (var file in files.Where(file => deletePaths.Contains(file.Path)))
        {
            try
            {
                File.Delete(file.Path);
                deleted++;
                freedBytes += file.SizeBytes;
            }
            catch (Exception exception)
            {
                errors.Add($"{file.Path}: {exception.Message}");
            }
        }

        return new ScreenshotCleanupResult(
            files.Count,
            deleted,
            files.Count(file => protectedSet.Contains(file.Path)),
            freedBytes,
            Math.Max(0, files.Sum(file => file.SizeBytes) - freedBytes),
            errors);
    }

    private List<ScreenshotStorageFile> EnumerateFiles()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        var result = new List<ScreenshotStorageFile>();
        foreach (var path in Directory.EnumerateFiles(
                     _rootDirectory,
                     "*.png",
                     SearchOption.AllDirectories))
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var info = new FileInfo(fullPath);
                result.Add(new ScreenshotStorageFile(
                    fullPath,
                    info.Length,
                    info.LastWriteTimeUtc,
                    false));
            }
            catch (Exception exception)
            {
                AppLog.Warn(
                    $"Не удалось прочитать снимок «{path}»: " +
                    exception.Message);
            }
        }

        return result;
    }

    private static HashSet<string> NormalizePaths(
        IEnumerable<string>? paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (paths is null)
        {
            return result;
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                result.Add(Path.GetFullPath(path));
            }
            catch
            {
                // Некорректная ссылка не должна останавливать очистку.
            }
        }

        return result;
    }
}
