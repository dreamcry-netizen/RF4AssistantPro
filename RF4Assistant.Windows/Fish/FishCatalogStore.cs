using System.IO;
using System.Text.Json;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Fish;

public sealed class FishCatalogStore
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

    public FishCatalogStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "Fish");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(ImagesDirectory);
    }

    public string CatalogPath => Path.Combine(_root, "catalog.json");

    public string ImagesDirectory => Path.Combine(_root, "Images");

    public List<FishCatalogItem> LoadCatalog()
    {
        EnsureSeed();
        return Load<List<FishCatalogItem>>(CatalogPath) ?? [];
    }

    public void SaveCatalog(IEnumerable<FishCatalogItem> items)
    {
        Save(CatalogPath, items
            .OrderBy(item => item.Family)
            .ThenBy(item => item.Name)
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

        using (var image = System.Drawing.Image.FromFile(sourcePath))
        {
            if (image.Width <= 0 || image.Height <= 0)
            {
                throw new InvalidDataException(
                    "Файл не является корректным изображением.");
            }
        }

        var target = Path.Combine(
            ImagesDirectory,
            $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, target, false);
        return target;
    }

    private void EnsureSeed()
    {
        if (File.Exists(CatalogPath))
        {
            return;
        }

        SaveCatalog(
        [
            new FishCatalogItem
            {
                Name = "Карась серебряный",
                Family = "Карповые",
                Habitat = "Озёра и реки",
                Aliases = "Карась; Серебряный карась"
            },
            new FishCatalogItem
            {
                Name = "Окунь",
                Family = "Окуневые",
                Habitat = "Озёра и реки",
                Aliases = "Окунь обыкновенный"
            },
            new FishCatalogItem
            {
                Name = "Щука обыкновенная",
                Family = "Щуковые",
                Habitat = "Озёра и реки",
                Aliases = "Щука"
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
