using System.IO;

namespace RF4AssistantPro.Services;

public sealed class CafeScreenshotImportService
{
    private readonly string _targetDirectory;

    public CafeScreenshotImportService(string? targetDirectory = null)
    {
        _targetDirectory = targetDirectory ??
            PortableDataPaths.GetScreenshotDirectory("rf4_cafe");
    }

    public string Import(string sourcePath, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                "Не найден выбранный снимок кафе.",
                sourcePath);
        }

        if (!Path.GetExtension(sourcePath).Equals(
                ".png",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Для повторного OCR выберите снимок кафе в формате PNG.");
        }

        if (new FileInfo(sourcePath).Length == 0)
        {
            throw new InvalidDataException("Выбранный PNG-файл пуст.");
        }

        Directory.CreateDirectory(_targetDirectory);
        var timestamp = now ?? DateTime.Now;
        var targetPath = Path.Combine(
            _targetDirectory,
            $"rf4_cafe_import_{timestamp:yyyyMMdd_HHmmss_fff}_" +
            $"{Guid.NewGuid():N}.png");
        File.Copy(sourcePath, targetPath, false);
        return targetPath;
    }
}
