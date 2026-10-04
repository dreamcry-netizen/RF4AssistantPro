using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RF4AssistantPro.Services;

public static class AppLog
{
    private static readonly object Sync = new();
    private const long MaxLogBytes = 5L * 1024 * 1024;

    // Портативная сборка хранит журнал рядом с EXE, чтобы его можно было
    // быстро найти и отправить вместе с программой без поиска в AppData.
    public static string DirectoryPath { get; } =
        PortableDataPaths.LogsDirectory;

    public static string EventsFilePath { get; } = Path.Combine(
        DirectoryPath,
        "Events.log");

    public static string ErrorsFilePath { get; } = Path.Combine(
        DirectoryPath,
        "Errors.log");

    public static string ScreenshotsFilePath { get; } = Path.Combine(
        DirectoryPath,
        "Screenshots.log");

    // Оставлено для совместимости с интерфейсом и сообщениями старых версий.
    public static string FilePath => EventsFilePath;

    public static void Info(string message)
    {
        Write(
            IsScreenshotMessage(message)
                ? ScreenshotsFilePath
                : EventsFilePath,
            "INFO",
            message);
    }

    public static void Warn(string message)
    {
        Write(ErrorsFilePath, "WARN", message);
        if (IsScreenshotMessage(message))
        {
            Write(ScreenshotsFilePath, "WARN", message);
        }
    }

    public static bool DetailedOcrLoggingEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("RF4ASSISTANT_DETAILED_OCR_LOG"),
            "1",
            StringComparison.Ordinal);

    public static void OcrDetails(string message)
    {
        if (DetailedOcrLoggingEnabled)
        {
            Write(ScreenshotsFilePath, "OCR", message);
        }
    }

    public static void Error(string message, Exception? exception = null)
    {
        var details = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";
        Write(ErrorsFilePath, "ERROR", details);
        if (IsScreenshotMessage(message))
        {
            Write(ScreenshotsFilePath, "ERROR", details);
        }
    }

    private static void Write(
        string filePath,
        string level,
        string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                RotateIfNeeded(filePath);
                File.AppendAllText(
                    filePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                    $"[{level}] {RedactSensitiveData(message)}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Ошибка журнала не должна мешать запуску приложения.
        }
    }

    private static bool IsScreenshotMessage(string message)
    {
        string[] markers =
        [
            "снимок",
            "скриншот",
            "ocr",
            "карточк",
            "распознан",
            "capture",
            "rf4_catch_",
            "rf4_waterbody_",
            "rf4_bait_",
            "rf4_keepnet_",
            "rf4_cafe_",
            "подготовка улова",
            "сопоставления улова",
            "улов автоматически",
            "улов сохранён",
            "садок сохранён",
            "кафе:"
        ];

        return markers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string RedactSensitiveData(string message)
    {
        var redacted = Regex.Replace(
            message,
            @"(?i)([A-Z]:\\Users\\)[^\\\r\n]+",
            "$1<user>");
        redacted = Regex.Replace(
            redacted,
            @"(?i)(/home/)[^/\r\n]+",
            "$1<user>");
        var baseDirectory = PortableDataPaths.BaseDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return baseDirectory.Length == 0
            ? redacted
            : Regex.Replace(
                redacted,
                Regex.Escape(baseDirectory),
                "<app>",
                RegexOptions.IgnoreCase);
    }

    private static void RotateIfNeeded(string filePath)
    {
        if (!File.Exists(filePath) ||
            new FileInfo(filePath).Length < MaxLogBytes)
        {
            return;
        }

        var previousPath = Path.Combine(
            DirectoryPath,
            $"{Path.GetFileNameWithoutExtension(filePath)}.previous.log");
        File.Move(
            filePath,
            previousPath,
            true);
    }
}