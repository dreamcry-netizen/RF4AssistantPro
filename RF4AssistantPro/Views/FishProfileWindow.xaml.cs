using RF4AssistantPro.Fish;
using RF4AssistantPro.Models;

namespace RF4AssistantPro.Views;

public partial class FishProfileWindow : System.Windows.Window
{
    public FishProfileWindow(
        FishCatalogItem fish,
        IEnumerable<CatchRecord> catches)
    {
        InitializeComponent();
        DataContext = new FishProfileViewModel(fish, catches);
        Title = $"Карточка рыбы — {fish.Name}";
    }

    private void TitleBarMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as System.Windows.DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == System.Windows.WindowState.Maximized
                ? System.Windows.WindowState.Normal
                : System.Windows.WindowState.Maximized;
            e.Handled = true;
            return;
        }

        if (e.LeftButton ==
            System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
            e.Handled = true;
        }
    }

    private void CloseClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    private static bool IsInsideButton(
        System.Windows.DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}

public sealed class FishProfileViewModel
{
    private readonly FishCatalogItem _fish;

    public FishProfileViewModel(
        FishCatalogItem fish,
        IEnumerable<CatchRecord> catches)
    {
        _fish = fish;
        BaitStatistics = catches
            .Where(record =>
                record.FishName.Equals(
                    fish.Name,
                    StringComparison.OrdinalIgnoreCase))
            .GroupBy(record =>
                string.IsNullOrWhiteSpace(record.BaitName)
                    ? "Наживка не указана"
                    : record.BaitName.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new FishBaitStatistic(
                group.Key,
                group.ToList()))
            .OrderByDescending(item => item.CatchCount)
            .ThenByDescending(item => item.BestWeightKg)
            .ThenBy(item => item.BaitName)
            .ToList();
    }

    public string Name => _fish.Name;

    public string ImagePath => _fish.ImagePath;

    public string RarityDisplay => _fish.RarityDisplay;

    public string TrophyWeightDisplay => _fish.TrophyWeightDisplay;

    public string BlueTrophyWeightDisplay =>
        _fish.BlueTrophyWeightDisplay;

    public string MinimumWeightDisplay => _fish.MinimumWeightDisplay;

    public string QualifyingWeightDisplay =>
        _fish.QualifyingWeightDisplay;

    public string ChatWeightDisplay => _fish.ChatWeightDisplay;

    public string MaximumWeightDisplay => _fish.MaximumWeightDisplay;

    public string Family => _fish.Family;

    public string WaterBodiesDisplay => _fish.WaterBodiesDisplay;

    public IReadOnlyList<string> WaterBodyNames => _fish.WaterBodyNames;

    public string WaterBodyCountDisplay => _fish.WaterBodyCountDisplay;

    public string BiteActivityDisplay => _fish.BiteActivityDisplay;

    public string PopulationDisplay => _fish.PopulationDisplay;

    public string LayerDisplay => _fish.LayerDisplay;

    public string BestTimeDisplay => _fish.BestTimeDisplay;

    public string TrophyTypeDisplay => _fish.TrophyTypeDisplay;

    public string HookDisplay => _fish.HookDisplay;

    public string LeaderDisplay => _fish.LeaderDisplay;

    public string CardPriceDisplay => _fish.CardPriceDisplay;

    public string BaitHintsDisplay => _fish.BaitHintsDisplay;

    public IReadOnlyList<string> ReferenceBaitNames =>
        _fish.ReferenceBaitNames;

    public string DescriptionDisplay => _fish.DescriptionDisplay;

    public string TipsDisplay => _fish.TipsDisplay;

    public IReadOnlyList<FishRateBand> RateBandsDisplay =>
        _fish.RateBandsDisplay;

    public IReadOnlyList<FishBaitStatistic> BaitStatistics { get; }

    public string BaitStatisticsSummary =>
        BaitStatistics.Count == 0
            ? "По этой рыбе пока нет сохранённых уловов."
            : $"Наживок: {BaitStatistics.Count}; " +
              $"уловов: {BaitStatistics.Sum(item => item.CatchCount)}.";
}

public sealed class FishBaitStatistic
{
    private readonly IReadOnlyList<CatchRecord> _records;

    public FishBaitStatistic(
        string baitName,
        IReadOnlyList<CatchRecord> records)
    {
        BaitName = baitName;
        _records = records;
        BestWeightKg = records.Count == 0
            ? 0m
            : records.Max(record => record.WeightKg);
        AverageWeightKg = records.Count == 0
            ? 0m
            : records.Average(record => record.WeightKg);
        LastCaughtAt = records.Count == 0
            ? null
            : records.Max(record => record.CaughtAt);
    }

    public string BaitName { get; }

    public int CatchCount => _records.Count;

    public decimal BestWeightKg { get; }

    public decimal AverageWeightKg { get; }

    public DateTime? LastCaughtAt { get; }

    public string CatchCountDisplay =>
        $"{CatchCount} {Pluralize(CatchCount, "улов", "улова", "уловов")}";

    public string BestWeightDisplay =>
        WeightDisplayFormatter.Format(BestWeightKg);

    public string AverageWeightDisplay =>
        WeightDisplayFormatter.Format(AverageWeightKg);

    public string LastCaughtDisplay =>
        LastCaughtAt.HasValue
            ? LastCaughtAt.Value.ToString("dd.MM.yyyy HH:mm")
            : "—";

    private static string Pluralize(
        int value,
        string one,
        string few,
        string many)
    {
        var lastTwo = value % 100;
        if (lastTwo is >= 11 and <= 19)
        {
            return many;
        }

        return (value % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }
}