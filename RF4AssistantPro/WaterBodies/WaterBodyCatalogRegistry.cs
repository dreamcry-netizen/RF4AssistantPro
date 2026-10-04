using RF4AssistantPro.Statistics;

namespace RF4AssistantPro.WaterBodies;

public sealed record WaterBodyCatalogDefinition(
    int Order,
    string Name,
    string CardImagePath);

public sealed class WaterBodyCatalogItem
{
    public WaterBodyCatalogItem(
        WaterBodyCatalogDefinition definition,
        WaterBodyRating statistics)
    {
        Definition = definition;
        Statistics = statistics;
    }

    public WaterBodyCatalogDefinition Definition { get; }
    public WaterBodyRating Statistics { get; }
    public int Order => Definition.Order;
    public string Name => Definition.Name;
    public string CardImagePath => Definition.CardImagePath;
    public string CafeTitle => $"Кафе · {Name}";
}

public static class WaterBodyCatalogRegistry
{
    private const string AssetRoot =
        "/RF4AssistantPro;component/Assets/WaterBodies/Cards/";

    public static IReadOnlyList<WaterBodyCatalogDefinition> Definitions { get; } =
    [
        new(1, "Комариное", AssetRoot + "01_Komarinoe.png"),
        new(2, "Лосиное", AssetRoot + "02_Losinoe.png"),
        new(3, "Вьюнок", AssetRoot + "03_Vyunok.png"),
        new(4, "Старый острог", AssetRoot + "04_StaryOstrog.png"),
        new(5, "Белая", AssetRoot + "05_Belaya.png"),
        new(6, "Куори", AssetRoot + "06_Kuori.png"),
        new(7, "Медвежье", AssetRoot + "07_Medvezhye.png"),
        new(8, "Волхов", AssetRoot + "08_Volkhov.png"),
        new(9, "Северский Донец", AssetRoot + "09_SeverskyDonets.png"),
        new(10, "Сура", AssetRoot + "10_Sura.png"),
        new(11, "Ладожское озеро", AssetRoot + "11_LadogaLake.png"),
        new(12, "Янтарное", AssetRoot + "12_Yantarnoe.png"),
        new(13, "Ладожский архипелаг", AssetRoot + "13_LadogaArchipelago.png"),
        new(14, "Ахтуба", AssetRoot + "14_Akhtuba.png"),
        new(15, "Медное", AssetRoot + "15_Mednoe.png"),
        new(16, "Нижняя Тунгуска", AssetRoot + "16_LowerTunguska.png"),
        new(17, "Яма", AssetRoot + "17_Yama.png"),
        new(18, "Норвежское море", AssetRoot + "18_NorwegianSea.png")
    ];

    public static bool NamesMatch(string left, string right) =>
        Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ё", "е", StringComparison.Ordinal);
        foreach (var prefix in new[] { "оз.", "озеро", "р.", "река" })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
            }
        }
        return string.Concat(normalized.Where(char.IsLetterOrDigit));
    }
}
