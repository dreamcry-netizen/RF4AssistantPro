using System.Windows.Media;
using RF4AssistantPro.Models;
using WpfBrush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace RF4AssistantPro.ViewModels;

public sealed class KeepnetComparisonRowViewModel
{
    public string FishName { get; init; } = "";

    public string FishImagePath { get; init; } = "";

    public decimal WeightKg { get; init; }

    public string WeightDisplay => WeightDisplayFormatter.Format(WeightKg);

    public int HistoryCount { get; init; }

    public int KeepnetCount { get; init; }

    public int Difference => KeepnetCount - HistoryCount;

    public string DifferenceDisplay =>
        Difference > 0 ? $"+{Difference}" : Difference.ToString();

    public string Status { get; init; } = "";

    public WpfBrush StatusBrush { get; init; } = Brushes.LightGray;
}