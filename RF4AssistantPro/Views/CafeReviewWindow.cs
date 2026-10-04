using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RF4AssistantPro.Cafe;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCheckBoxColumn = System.Windows.Controls.DataGridCheckBoxColumn;
using DataGridComboBoxColumn = System.Windows.Controls.DataGridComboBoxColumn;
using DataGridLength = System.Windows.Controls.DataGridLength;
using DataGridTextColumn = System.Windows.Controls.DataGridTextColumn;
using DockPanel = System.Windows.Controls.DockPanel;
using Grid = System.Windows.Controls.Grid;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using RowDefinition = System.Windows.Controls.RowDefinition;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using MediaColor = System.Windows.Media.Color;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfDataGridLengthUnitType =
    System.Windows.Controls.DataGridLengthUnitType;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfUpdateSourceTrigger =
    System.Windows.Data.UpdateSourceTrigger;

namespace RF4AssistantPro.Views;

public sealed class CafeReviewWindow : Window
{
    private readonly ObservableCollection<CafeOfferReviewRow> _rows = [];
    private readonly DataGrid _grid = new();

    public CafeReviewWindow(
        string screenshotPath,
        IReadOnlyList<CafeOffer> offers)
    {
        Title = "Проверка предложений кафе";
        Width = 1040;
        Height = 760;
        MinWidth = 820;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(MediaColor.FromRgb(7, 17, 15));
        Foreground = new SolidColorBrush(MediaColor.FromRgb(240, 242, 232));

        foreach (var offer in offers)
        {
            _rows.Add(CafeOfferReviewRow.FromOffer(offer));
        }

        if (_rows.Count == 0)
        {
            _rows.Add(new CafeOfferReviewRow());
        }

        Content = BuildContent(screenshotPath, offers);
    }

    public IReadOnlyList<CafeOffer> Result { get; private set; } = [];

    private FrameworkElement BuildContent(
        string screenshotPath,
        IReadOnlyList<CafeOffer> offers)
    {
        var root = new Grid
        {
            Margin = new Thickness(18)
        };
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(210)
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 12)
        };
        if (File.Exists(screenshotPath))
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(
                Path.GetFullPath(screenshotPath),
                UriKind.Absolute);
            source.EndInit();
            source.Freeze();
            image.Source = source;
        }

        Grid.SetRow(image, 0);
        root.Children.Add(image);

        var recognizedCount = offers.Count(offer =>
            !string.IsNullOrWhiteSpace(offer.FishName));
        var noticePrefix = recognizedCount < offers.Count
            ? $"Распознано карточек: {recognizedCount} из {offers.Count}. " +
              "Пропущенные строки оставлены для ручного ввода. "
            : $"Распознано карточек: {recognizedCount} из {offers.Count}. ";
        var notice = new TextBlock
        {
            Text = noticePrefix +
                   "Проверьте название, количество и порог. " +
                   "Для каждой сохранённой строки обязательна единица г/кг. " +
                   "Ошибочную карточку можно отключить.",
            Foreground = new SolidColorBrush(
                MediaColor.FromRgb(242, 194, 102)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(notice, 1);
        root.Children.Add(notice);

        ConfigureGrid();
        _grid.ItemsSource = _rows;
        Grid.SetRow(_grid, 2);
        root.Children.Add(_grid);

        var footer = new DockPanel
        {
            Margin = new Thickness(0, 14, 0, 0)
        };
        var rowButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        var add = new Button
        {
            Content = "Добавить строку",
            MinWidth = 125,
            Height = 36
        };
        add.Click += (_, _) =>
        {
            var row = new CafeOfferReviewRow();
            _rows.Add(row);
            _grid.SelectedItem = row;
            _grid.ScrollIntoView(row);
        };
        var remove = new Button
        {
            Content = "Удалить выбранную",
            MinWidth = 145,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0)
        };
        remove.Click += (_, _) =>
        {
            if (_grid.SelectedItem is CafeOfferReviewRow selected)
            {
                _rows.Remove(selected);
            }
        };
        var alternative = new Button
        {
            Content = "Подставить альтернативный вес",
            MinWidth = 205,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0)
        };
        alternative.Click += (_, _) =>
        {
            if (_grid.SelectedItem is not CafeOfferReviewRow selected ||
                !selected.UseAlternativeWeight())
            {
                MessageBox.Show(
                    this,
                    "У выбранной строки нет конфликтующего значения OCR.",
                    "Проверка кафе",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _grid.Items.Refresh();
        };
        rowButtons.Children.Add(add);
        rowButtons.Children.Add(remove);
        rowButtons.Children.Add(alternative);
        DockPanel.SetDock(rowButtons, System.Windows.Controls.Dock.Left);
        footer.Children.Add(rowButtons);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Отмена",
            MinWidth = 110,
            Height = 36,
            IsCancel = true
        };
        var save = new Button
        {
            Content = "Сохранить кафе",
            MinWidth = 145,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0)
        };
        save.Click += SaveClick;
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        return root;
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.CanUserAddRows = false;
        _grid.CanUserDeleteRows = false;
        _grid.HeadersVisibility =
            System.Windows.Controls.DataGridHeadersVisibility.Column;
        _grid.GridLinesVisibility =
            System.Windows.Controls.DataGridGridLinesVisibility.Horizontal;
        _grid.RowHeaderWidth = 0;
        _grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Сохранить",
            Width = new DataGridLength(78),
            Binding = new WpfBinding(nameof(CafeOfferReviewRow.Include))
        });
        _grid.Columns.Add(TextColumn(
            "Рыба",
            nameof(CafeOfferReviewRow.FishName),
            2.1));
        _grid.Columns.Add(TextColumn(
            "Водоём",
            nameof(CafeOfferReviewRow.WaterBodyName),
            1.5));
        _grid.Columns.Add(TextColumn(
            "Количество",
            nameof(CafeOfferReviewRow.Quantity),
            0.9));
        _grid.Columns.Add(TextColumn(
            "Масса от",
            nameof(CafeOfferReviewRow.MinimumWeight),
            1.0));
        _grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Ед.",
            Width = new DataGridLength(75),
            ItemsSource = new[] { "г", "кг" },
            SelectedItemBinding = new WpfBinding(
                nameof(CafeOfferReviewRow.WeightUnit))
        });
        _grid.Columns.Add(TextColumn(
            "Источник OCR",
            nameof(CafeOfferReviewRow.SourceSummary),
            1.8,
            true));
    }

    private static DataGridTextColumn TextColumn(
        string header,
        string property,
        double width,
        bool readOnly = false)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Width = new DataGridLength(
                width,
                WpfDataGridLengthUnitType.Star),
            IsReadOnly = readOnly,
            Binding = new WpfBinding(property)
            {
                UpdateSourceTrigger =
                    WpfUpdateSourceTrigger.PropertyChanged
            },
            ElementStyle = readOnly
                ? CreateReadOnlyTextStyle()
                : null
        };
    }

    private static Style CreateReadOnlyTextStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(
            TextBlock.TextWrappingProperty,
            TextWrapping.Wrap));
        style.Setters.Add(new Setter(
            TextBlock.TextTrimmingProperty,
            TextTrimming.None));
        style.Setters.Add(new Setter(
            TextBlock.VerticalAlignmentProperty,
            VerticalAlignment.Center));
        return style;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        _grid.CommitEdit(
            System.Windows.Controls.DataGridEditingUnit.Cell,
            true);
        _grid.CommitEdit(
            System.Windows.Controls.DataGridEditingUnit.Row,
            true);

        var result = new List<CafeOffer>();
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            if (!row.Include)
            {
                continue;
            }

            if (!CafeOfferReviewParser.TryCreate(
                    new CafeOfferReviewInput(
                        row.FishName,
                        row.WaterBodyName,
                        row.Quantity,
                        row.MinimumWeight,
                        row.WeightUnit,
                        row.Price,
                        row.WeightSource,
                        row.RawWeightText,
                        row.AlternativeMinimumWeightGrams,
                        row.AlternativeWeightUnit,
                        row.AlternativeRawWeightText,
                        row.AlternativeWeightSource,
                        row.Id,
                        row.RawFishName,
                        row.FishCatalogId,
                        row.RecognitionSource),
                    out var offer,
                    out var error))
            {
                _grid.SelectedItem = row;
                _grid.ScrollIntoView(row);
                MessageBox.Show(
                    this,
                    $"Строка {index + 1}: {error}.",
                    "Проверка кафе",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            result.Add(offer!);
        }

        if (result.Count == 0)
        {
            MessageBox.Show(
                this,
                "Оставьте хотя бы одно предложение для сохранения.",
                "Проверка кафе",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Result = result;
        DialogResult = true;
    }
}

public sealed class CafeOfferReviewRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Include { get; set; } = true;

    public string FishName { get; set; } = "";

    public string RawFishName { get; set; } = "";

    public string FishCatalogId { get; set; } = "";

    public string RecognitionSource { get; set; } = "";

    public string WaterBodyName { get; set; } = "";

    public string Quantity { get; set; } = "";

    public string MinimumWeight { get; set; } = "";

    public string WeightUnit { get; set; } = "";

    public decimal? Price { get; set; }

    public string WeightSource { get; set; } = "";

    public string RawWeightText { get; set; } = "";

    public decimal? AlternativeMinimumWeightGrams { get; set; }

    public string AlternativeWeightUnit { get; set; } = "";

    public string AlternativeRawWeightText { get; set; } = "";

    public string AlternativeWeightSource { get; set; } = "";

    public string SourceSummary
    {
        get
        {
            var selected = string.IsNullOrWhiteSpace(RecognitionSource)
                ? string.IsNullOrWhiteSpace(WeightSource)
                    ? "источник не указан"
                    : WeightSource
                : RecognitionSource;
            if (!string.IsNullOrWhiteSpace(RawFishName) &&
                !string.Equals(RawFishName, FishName, StringComparison.Ordinal))
            {
                selected += $" · OCR-название: «{RawFishName}»";
            }
            if (!string.IsNullOrWhiteSpace(RawWeightText))
            {
                selected += $" · вес «{RawWeightText}»";
            }

            if (!AlternativeMinimumWeightGrams.HasValue)
            {
                return selected;
            }

            return selected +
                   $" · альтернатива: {FormatGrams(
                       AlternativeMinimumWeightGrams.Value,
                       AlternativeWeightUnit)} " +
                   $"({AlternativeWeightSource})";
        }
    }

    public static CafeOfferReviewRow FromOffer(CafeOffer offer)
    {
        var normalizedFish = CafeFishNameCanonicalizer.Canonicalize(
            offer.FishName,
            []);
        var weight = offer.MinimumWeightGrams;
        var useKilograms = offer.MinimumWeightUnit == "кг" ||
                           string.IsNullOrWhiteSpace(
                               offer.MinimumWeightUnit) &&
                           weight >= 1000m;
        return new CafeOfferReviewRow
        {
            Id = offer.Id == Guid.Empty ? Guid.NewGuid() : offer.Id,
            FishName = normalizedFish.Name,
            RawFishName = string.IsNullOrWhiteSpace(offer.RawFishName)
                ? normalizedFish.RawName
                : offer.RawFishName,
            FishCatalogId = offer.FishCatalogId,
            RecognitionSource = offer.RecognitionSource,
            WaterBodyName = offer.WaterBodyName,
            Quantity = offer.Quantity > 0
                ? offer.Quantity.ToString(CultureInfo.InvariantCulture)
                : "",
            MinimumWeight = weight.HasValue
                ? (useKilograms
                    ? weight.Value / 1000m
                    : weight.Value).ToString(
                        useKilograms ? "0.###" : "0.##",
                        CultureInfo.InvariantCulture)
                : "",
            WeightUnit = weight.HasValue
                ? !string.IsNullOrWhiteSpace(offer.MinimumWeightUnit)
                    ? offer.MinimumWeightUnit
                    : useKilograms ? "кг" : "г"
                : "",
            WeightSource = offer.WeightSource,
            RawWeightText = offer.RawWeightText,
            AlternativeMinimumWeightGrams =
                offer.AlternativeMinimumWeightGrams,
            AlternativeWeightUnit =
                offer.AlternativeMinimumWeightUnit,
            AlternativeRawWeightText =
                offer.AlternativeRawWeightText,
            AlternativeWeightSource =
                offer.AlternativeWeightSource,
            Price = offer.Price
        };
    }

    public bool UseAlternativeWeight()
    {
        if (AlternativeMinimumWeightGrams is not { } grams)
        {
            return false;
        }

        var unit = AlternativeWeightUnit is "г" or "кг"
            ? AlternativeWeightUnit
            : grams >= 1000m ? "кг" : "г";
        MinimumWeight = (unit == "кг"
                ? grams / 1000m
                : grams)
            .ToString(
                unit == "кг" ? "0.###" : "0.##",
                CultureInfo.InvariantCulture);
        WeightUnit = unit;
        WeightSource = AlternativeWeightSource;
        RawWeightText = AlternativeRawWeightText;
        AlternativeMinimumWeightGrams = null;
        AlternativeWeightUnit = "";
        AlternativeRawWeightText = "";
        AlternativeWeightSource = "";
        return true;
    }

    private static string FormatGrams(decimal grams, string unit)
    {
        return unit == "кг"
            ? $"{grams / 1000m:0.###} кг"
            : $"{grams:0.##} г";
    }
}