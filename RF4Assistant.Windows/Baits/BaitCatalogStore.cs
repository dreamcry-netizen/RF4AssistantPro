using System.IO;
using System.Text.Json;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Baits;

public sealed class BaitCatalogStore
{
    private const long MaxImageBytes = 10L * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _root;

    public BaitCatalogStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Baits");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(ImagesDirectory);
    }

    public string CatalogPath => Path.Combine(_root, "catalog.json");

    public string UnrecognizedPath => Path.Combine(_root, "unrecognized.json");

    public string ImagesDirectory => Path.Combine(_root, "Images");

    public List<BaitCatalogItem> LoadCatalog()
    {
        EnsureSeed();
        return Load<List<BaitCatalogItem>>(CatalogPath) ?? [];
    }

    public void SaveCatalog(IEnumerable<BaitCatalogItem> items)
    {
        Save(CatalogPath, items.OrderBy(item => item.Category)
            .ThenBy(item => item.Subcategory)
            .ThenBy(item => item.DisplayName)
            .ToList());
    }

    public List<UnrecognizedBait> LoadUnrecognized()
    {
        return Load<List<UnrecognizedBait>>(UnrecognizedPath) ?? [];
    }

    public void SaveUnrecognized(IEnumerable<UnrecognizedBait> items)
    {
        Save(UnrecognizedPath, items
            .OrderByDescending(item => item.CapturedAt)
            .Take(100)
            .ToList());
    }

    public string ImportImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return "";
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedImageExtensions.Contains(extension))
        {
            throw new InvalidDataException(
                "Поддерживаются изображения PNG, JPG, JPEG, BMP и GIF.");
        }

        if (new FileInfo(sourcePath).Length > MaxImageBytes)
        {
            throw new InvalidDataException(
                "Размер изображения не должен превышать 10 МБ.");
        }

        // Проверяем фактическое содержимое, а не только расширение.
        using (var image = System.Drawing.Image.FromFile(sourcePath))
        {
            if (image.Width <= 0 || image.Height <= 0)
            {
                throw new InvalidDataException("Файл не является корректным изображением.");
            }
        }

        var target = Path.Combine(
            ImagesDirectory,
            $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, target, false);
        return target;
    }

    public void AddUnrecognized(
        string candidateName,
        string rawText,
        string screenshotPath)
    {
        var items = LoadUnrecognized();
        items.Insert(0, new UnrecognizedBait
        {
            CandidateName = candidateName,
            RawText = rawText,
            ScreenshotPath = screenshotPath
        });
        SaveUnrecognized(items);
        AppLog.Info($"Нераспознанная наживка добавлена в очередь: {candidateName}.");
    }

    private void EnsureSeed()
    {
        if (File.Exists(CatalogPath))
        {
            return;
        }

        var seedImage = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Baits",
            "worm.png");
        var storedImage = File.Exists(seedImage)
            ? ImportImage(seedImage)
            : "";

        SaveCatalog(
        [
            new BaitCatalogItem
            {
                Name = "Червь",
                Brand = "Nature",
                Category = "Натуральные наживки",
                Subcategory = "Черви",
                Aliases = "Червяк; Nature Червь; Черв",
                ImagePath = storedImage
            }
        ]);
    }

    private static T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            JsonOptions);
    }

    private static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, true);
    }
}