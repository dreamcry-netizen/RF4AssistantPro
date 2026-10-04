using System.IO;

namespace RF4AssistantPro.Services;

public sealed class DiagnosticSummary
{
    public string EnvironmentText { get; init; } = "";

    public string StorageText { get; init; } = "";

    public string MediaText { get; init; } = "";

    public string LogText { get; init; } = "";

    public string HealthText { get; init; } = "";
}

public static class DiagnosticSummaryService
{
    public static DiagnosticSummary Collect()
    {
        var screenshotCount = 0;
        long screenshotBytes = 0;
        var mediaError = "";

        try
        {
            if (Directory.Exists(PortableDataPaths.ScreenshotsDirectory))
            {
                foreach (var path in Directory.EnumerateFiles(
                             PortableDataPaths.ScreenshotsDirectory,
                             "*.png",
                             SearchOption.AllDirectories))
                {
                    screenshotCount++;
                    screenshotBytes += new FileInfo(path).Length;
                }
            }
        }
        catch (Exception exception)
        {
            mediaError = exception.Message;
        }

        var logFiles = new[]
        {
            AppLog.EventsFilePath,
            AppLog.ErrorsFilePath,
            AppLog.ScreenshotsFilePath
        };
        var existingLogs = logFiles.Count(File.Exists);
        var logBytes = logFiles
            .Where(File.Exists)
            .Sum(path => new FileInfo(path).Length);
        var lastError = File.Exists(AppLog.ErrorsFilePath)
            ? File.GetLastWriteTime(AppLog.ErrorsFilePath)
            : (DateTime?)null;

        var storagePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro");
        var storageStatus = Directory.Exists(storagePath)
            ? "каталог данных создан"
            : "каталог данных будет создан при первой записи";

        var entryVersion = System.Reflection.Assembly
            .GetEntryAssembly()?
            .GetName()
            .Version;
        var versionNumber = entryVersion is null
            ? "неизвестно"
            : $"{entryVersion.Major}.{entryVersion.Minor}.{entryVersion.Build}";

        return new DiagnosticSummary
        {
            EnvironmentText =
                $"Версия {versionNumber}; ОС {Environment.OSVersion}; " +
                $".NET {Environment.Version}; " +
                $"{(Environment.Is64BitProcess ? "x64" : "x86")}",
            StorageText =
                $"Данные: {storageStatus}. Логи: {AppLog.DirectoryPath}",
            MediaText = mediaError.Length == 0
                ? $"Снимков PNG: {screenshotCount}; " +
                  $"объём: {FormatBytes(screenshotBytes)}"
                : $"Снимки: ошибка чтения — {mediaError}",
            LogText =
                $"Файлов логов: {existingLogs}/3; " +
                $"объём: {FormatBytes(logBytes)}" +
                (lastError.HasValue
                    ? $"; последняя запись: {lastError.Value:dd.MM.yyyy HH:mm}"
                    : ""),
            HealthText = mediaError.Length == 0
                ? "Состояние диагностики: готово"
                : "Состояние диагностики: требуется проверка"
        };
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024L =>
                $"{bytes / 1024d / 1024d:0.##} МБ",
            >= 1024L =>
                $"{bytes / 1024d:0.##} КБ",
            _ => $"{bytes} Б"
        };
    }
}