using RF4AssistantPro.Cafe;
using RF4AssistantPro.Statistics;
using RF4AssistantPro.WaterBodies;

namespace RF4AssistantPro.ViewModels;

public sealed class WaterBodyDetailViewModel
{
    public WaterBodyDetailViewModel(
        WaterBodyRating statistics,
        IEnumerable<CafeSnapshot> cafeSnapshots)
        : this(
            new WaterBodyCatalogItem(
                new WaterBodyCatalogDefinition(0, statistics.Name, ""),
                statistics),
            cafeSnapshots)
    {
    }

    public WaterBodyDetailViewModel(
        WaterBodyCatalogItem waterBody,
        IEnumerable<CafeSnapshot> cafeSnapshots)
    {
        ArgumentNullException.ThrowIfNull(waterBody);
        var statistics = waterBody.Statistics;
        ArgumentNullException.ThrowIfNull(cafeSnapshots);

        WaterBody = waterBody;
        Statistics = statistics;
        var matchingSnapshot = cafeSnapshots
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                Offers = SelectCafeOffers(
                    snapshot.Offers,
                    statistics.Name)
            })
            .FirstOrDefault(item => item.Offers.Count > 0);

        if (matchingSnapshot is null)
        {
            CafeOffers = [];
            CafeCapturedAtDisplay = "Нет снимка для этого водоёма";
            CafeStatusDisplay = "Сделайте снимок кафе, чтобы увидеть заказы.";
            return;
        }

        CafeCapturedAtDisplay =
            matchingSnapshot.Snapshot.CapturedAt.ToString("dd.MM.yyyy HH:mm");
        var allCafeOffers = matchingSnapshot.Offers
            .Select(offer => new WaterBodyCafeOfferViewModel(
                offer,
                matchingSnapshot.Snapshot.OfferMatchedCounts.TryGetValue(
                    offer.Id,
                    out var matched)
                    ? matched
                    : 0))
            .ToList();
        CafeOffers = allCafeOffers;
        HiddenCafeOfferCount = 0;

        CafeRequestedCount = allCafeOffers
            .Where(offer => offer.Quantity > 0)
            .Sum(offer => offer.Quantity);
        CafeMatchedCount = allCafeOffers.Sum(offer => offer.MatchedCount);
        CafeRemainingCount = allCafeOffers.Sum(offer => offer.RemainingCount);
        var status = CafeRequestedCount > 0
            ? CafeRemainingCount == 0
                ? $"Заказ выполнен · {CafeMatchedCount} из {CafeRequestedCount}"
                : $"Осталось {CafeRemainingCount} из {CafeRequestedCount}"
            : "Количество рыб не распознано";
        CafeStatusDisplay = status;
    }

    public WaterBodyCatalogItem WaterBody { get; }

    public string Name => WaterBody.Name;

    public string CardImagePath => WaterBody.CardImagePath;

    public string CafeTitle => WaterBody.CafeTitle;

    public WaterBodyRating Statistics { get; }

    public IReadOnlyList<WaterBodyCafeOfferViewModel> CafeOffers { get; }

    public string CafeCapturedAtDisplay { get; }

    public string CafeStatusDisplay { get; }

    public int CafeRequestedCount { get; }

    public int CafeMatchedCount { get; }

    public int CafeRemainingCount { get; }

    public int HiddenCafeOfferCount { get; }

    public bool HasCafeOffers => CafeOffers.Count > 0;

    private static IReadOnlyList<CafeOffer> SelectCafeOffers(
        IReadOnlyList<CafeOffer> offers,
        string waterBodyName)
    {
        var matched = offers
            .Where(offer => WaterBodiesMatch(
                waterBodyName,
                offer.WaterBodyName))
            .ToList();
        if (matched.Count == 0)
        {
            return [];
        }

        // Один снимок кафе относится к выбранному водоёму. Если OCR не
        // повторил название локации в отдельных карточках, такие строки
        // должны остаться в том же снимке, а не исчезать из сводки.
        matched.AddRange(offers.Where(offer =>
            string.IsNullOrWhiteSpace(offer.WaterBodyName)));
        return matched
            .DistinctBy(offer => offer.Id)
            .ToList();
    }

    private static bool WaterBodiesMatch(string left, string right)
    {
        var normalizedLeft = NormalizeWaterBody(left);
        var normalizedRight = NormalizeWaterBody(right);
        return normalizedLeft.Length > 0 &&
               normalizedLeft.Equals(
                   normalizedRight,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWaterBody(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var prefix in new[] { "оз.", "озеро", "р.", "река" })
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
            }
        }

        return string.Join(
            ' ',
            normalized.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class WaterBodyCafeOfferViewModel
{
    public WaterBodyCafeOfferViewModel(CafeOffer offer, int matchedCount)
    {
        FishName = offer.FishName;
        Quantity = Math.Max(0, offer.Quantity);
        MatchedCount = Math.Max(0, matchedCount);
        RemainingCount = Quantity > 0
            ? Math.Max(0, Quantity - MatchedCount)
            : 0;
        MinimumWeightDisplay = FormatWeight(offer.MinimumWeightGrams);
        PriceDisplay = offer.Price.HasValue
            ? $"{offer.Price.Value:0.##} ₽"
            : "—";
    }

    public string FishName { get; }

    public int Quantity { get; }

    public int MatchedCount { get; }

    public int RemainingCount { get; }

    public string MinimumWeightDisplay { get; }

    public string PriceDisplay { get; }

    public string ProgressDisplay => Quantity > 0
        ? $"{MatchedCount}/{Quantity}"
        : "—";

    private static string FormatWeight(decimal? grams)
    {
        if (!grams.HasValue)
        {
            return "вес не распознан";
        }

        return grams.Value >= 1000m
            ? $"от {grams.Value / 1000m:0.###} кг"
            : $"от {grams.Value:0.#} г";
    }
}
