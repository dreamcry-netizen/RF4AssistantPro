using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Services;

public static class DiagnosticExportService
{
    public static string Create(bool includeRecentScreenshots)
    {
        var outputDirectory = Path.Combine(
            PortableDataPaths.BaseDirectory,
            "Diagnostics");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(
            outputDirectory,
            $"RF4AssistantPro_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        using var archive = ZipFile.Open(
            outputPath,
            ZipArchiveMode.Create);
        foreach (var logPath in new[]
                 {
                     AppLog.EventsFilePath,
                     AppLog.ErrorsFilePath,
                     AppLog.ScreenshotsFilePath
                 })
        {
            if (!File.Exists(logPath))
            {
                continue;
            }

            var entry = archive.CreateEntry(
                $"Logs/{Path.GetFileName(logPath)}",
                CompressionLevel.Optimal);
            using var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false));
            writer.Write(Sanitize(File.ReadAllText(logPath)));
        }

        if (includeRecentScreenshots &&
            Directory.Exists(PortableDataPaths.ScreenshotsDirectory))
        {
            foreach (var screenshot in Directory
                         .EnumerateFiles(
                             PortableDataPaths.ScreenshotsDirectory,
                             "*.png",
                             SearchOption.AllDirectories)
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Take(5))
            {
                var relativePath = Path.GetRelativePath(
                    PortableDataPaths.ScreenshotsDirectory,
                    screenshot);
                archive.CreateEntryFromFile(
                    screenshot,
                    $"Screenshots/{relativePath.Replace('\\', '/')}",
                    CompressionLevel.Optimal);
            }
        }

        var manifest = archive.CreateEntry(
            "README.txt",
            CompressionLevel.Optimal);
        using (var writer = new StreamWriter(
                   manifest.Open(),
                   new UTF8Encoding(false)))
        {
            writer.WriteLine("Диагностический архив RF4 Assistant Pro");
            writer.WriteLine($"Создан: {DateTime.Now:O}");
            writer.WriteLine(
                includeRecentScreenshots
                    ? "Добавлены пять последних снимков. Они могут содержать данные игрового профиля."
                    : "Снимки не добавлены.");
            writer.WriteLine(
                "Имена пользователей и путь установки удалены из журналов.");
        }

        return outputPath;
    }

    private static string Sanitize(string value)
    {
        var result = Regex.Replace(
            value,
            @"(?i)([A-Z]:\\Users\\)[^\\\r\n]+",
            "$1<user>");
        result = Regex.Replace(
            result,
            @"(?i)(/home/)[^/\r\n]+",
            "$1<user>");
        var baseDirectory = PortableDataPaths.BaseDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (baseDirectory.Length > 0)
        {
            result = Regex.Replace(
                result,
                Regex.Escape(baseDirectory),
                "<app>",
                RegexOptions.IgnoreCase);
        }

        return result;
    }
}