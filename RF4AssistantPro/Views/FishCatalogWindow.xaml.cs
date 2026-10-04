using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Views;

public partial class FishCatalogWindow : System.Windows.Window
{
    private readonly FishCatalogStore _store;
    private readonly ObservableCollection<FishCatalogItem> _catalog = [];
    private FishCatalogItem? _editing;
    private string _selectedImagePath = "";

    public FishCatalogWindow(
        FishCatalogStore store,
        bool openEditor = false,
        string? editItemId = null)
    {
        _store = store;
        InitializeComponent();
        CatalogGrid.ItemsSource = _catalog;
        Reload();

        if (openEditor)
        {
            NewClick(this, new System.Windows.RoutedEventArgs());
        }
        else if (!string.IsNullOrWhiteSpace(editItemId))
        {
            var item = _catalog.FirstOrDefault(value => value.Id == editItemId);
            if (item is not null)
            {
                CatalogGrid.SelectedItem = item;
                CatalogGrid.ScrollIntoView(item);
                LoadEditor(item);
            }
        }
    }

    public string CatalogCountText => $"Записей: {_catalog.Count}";

    private void Reload()
    {
        var editingId = _editing?.Id;
        _catalog.Clear();
        foreach (var item in _store.LoadCatalog())
        {
            _catalog.Add(item);
        }

        DataContext = null;
        DataContext = this;
        ApplySearch();
        if (!string.IsNullOrWhiteSpace(editingId))
        {
            var item = _catalog.FirstOrDefault(value => value.Id == editingId);
            if (item is not null)
            {
                LoadEditor(item);
            }
        }
    }

    private void SearchChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private void ApplySearch()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        CatalogGrid.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _catalog
            : _catalog.Where(item =>
                $"{item.Name} {item.Family} {item.Habitat} {item.Aliases}"
                    .Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
    }

    private void CatalogSelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is FishCatalogItem item)
        {
            LoadEditor(item);
        }
    }

    private void LoadEditor(FishCatalogItem item)
    {
        _editing = item;
        NameBox.Text = item.Name;
        FamilyBox.Text = item.Family;
        HabitatBox.Text = item.Habitat;
        TrophyWeightBox.Text = item.TrophyWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        AliasesBox.Text = item.Aliases;
        BlueTrophyWeightBox.Text = item.BlueTrophyWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        MinimumWeightBox.Text = item.MinimumWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        QualifyingWeightBox.Text = item.QualifyingWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        ChatWeightBox.Text = item.ChatWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        MaximumWeightBox.Text = item.MaximumWeightGrams?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        WaterBodiesBox.Text = item.WaterBodies;
        BiteActivityBox.Text = item.BiteActivity;
        PopulationBox.Text = item.Population;
        LayerBox.Text = item.Layer;
        BestTimeBox.Text = item.BestTime;
        TrophyTypeBox.Text = item.TrophyType;
        HookBox.Text = item.Hook;
        LeaderBox.Text = item.Leader;
        DescriptionBox.Text = item.Description;
        TipsBox.Text = item.Tips;
        BaitHintsBox.Text = item.BaitHints;
        CardPriceBox.Text = item.CardPrice?.ToString(
            CultureInfo.CurrentCulture) ?? "";
        LoadRateBandFields(item.RateBands);
        EnabledBox.IsChecked = item.IsEnabled;
        _selectedImagePath = item.ImagePath;
        PreviewImage.Source = LoadImage(item.ImagePath);
        EditorStatus.Text = $"Редактирование: {item.Name}";
    }

    private void NewClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _editing = null;
        NameBox.Clear();
        FamilyBox.Clear();
        HabitatBox.Clear();
        TrophyWeightBox.Clear();
        AliasesBox.Clear();
        BlueTrophyWeightBox.Clear();
        MinimumWeightBox.Clear();
        QualifyingWeightBox.Clear();
        ChatWeightBox.Clear();
        MaximumWeightBox.Clear();
        WaterBodiesBox.Clear();
        BiteActivityBox.Clear();
        PopulationBox.Clear();
        LayerBox.Clear();
        BestTimeBox.Clear();
        TrophyTypeBox.Clear();
        HookBox.Clear();
        LeaderBox.Clear();
        DescriptionBox.Clear();
        TipsBox.Clear();
        BaitHintsBox.Clear();
        CardPriceBox.Clear();
        ClearRateBandFields();
        EnabledBox.IsChecked = true;
        _selectedImagePath = "";
        PreviewImage.Source = null;
        EditorStatus.Text = "Новая запись.";
    }

    private void ChooseImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*",
            Title = "Изображение рыбы"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedImagePath = dialog.FileName;
        PreviewImage.Source = LoadImage(dialog.FileName);
    }

    private void SaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length < 2)
        {
            EditorStatus.Text = "Укажите название рыбы.";
            return;
        }

        if (!TryParseOptionalDecimal(
                TrophyWeightBox.Text,
                "Трофейный вес",
                out var trophyWeight,
                out var numericError) ||
            !TryParseOptionalDecimal(
                BlueTrophyWeightBox.Text,
                "Вес синего трофея",
                out var blueTrophyWeight,
                out numericError) ||
            !TryParseOptionalDecimal(
                MinimumWeightBox.Text,
                "Минимальный вес",
                out var minimumWeight,
                out numericError) ||
            !TryParseOptionalDecimal(
                QualifyingWeightBox.Text,
                "Зачётный вес",
                out var qualifyingWeight,
                out numericError) ||
            !TryParseOptionalDecimal(
                ChatWeightBox.Text,
                "Чатовый вес",
                out var chatWeight,
                out numericError) ||
            !TryParseOptionalDecimal(
                MaximumWeightBox.Text,
                "Максимальный вес",
                out var maximumWeight,
                out numericError) ||
            !TryParseOptionalDecimal(
                CardPriceBox.Text,
                "Цена карточки",
                out var cardPrice,
                out numericError))
        {
            EditorStatus.Text = numericError;
            return;
        }

        if (!TryReadRateBands(
                out var rateBands,
                out var rateBandsError))
        {
            EditorStatus.Text = rateBandsError;
            return;
        }

        var item = _editing ?? new FishCatalogItem();
        item.Name = name;
        item.Family = FamilyBox.Text.Trim();
        item.Habitat = HabitatBox.Text.Trim();
        item.TrophyWeightGrams = trophyWeight;
        item.Aliases = AliasesBox.Text.Trim();
        item.BlueTrophyWeightGrams = blueTrophyWeight;
        item.MinimumWeightGrams = minimumWeight;
        item.QualifyingWeightGrams = qualifyingWeight;
        item.ChatWeightGrams = chatWeight;
        item.MaximumWeightGrams = maximumWeight;
        item.WaterBodies = WaterBodiesBox.Text.Trim();
        item.BiteActivity = BiteActivityBox.Text.Trim();
        item.Population = PopulationBox.Text.Trim();
        item.Layer = LayerBox.Text.Trim();
        item.BestTime = BestTimeBox.Text.Trim();
        item.TrophyType = TrophyTypeBox.Text.Trim();
        item.Hook = HookBox.Text.Trim();
        item.Leader = LeaderBox.Text.Trim();
        item.Description = DescriptionBox.Text.Trim();
        item.Tips = TipsBox.Text.Trim();
        item.BaitHints = BaitHintsBox.Text.Trim();
        item.CardPrice = cardPrice;
        item.RateBands = rateBands;
        item.IsEnabled = EnabledBox.IsChecked == true;

        if (!string.IsNullOrWhiteSpace(_selectedImagePath) &&
            !string.Equals(
                _selectedImagePath,
                item.ImagePath,
                StringComparison.OrdinalIgnoreCase))
        {
            item.ImagePath = _store.ImportImage(_selectedImagePath);
        }

        var items = _catalog.ToList();
        if (_editing is null)
        {
            items.Add(item);
        }

        _store.SaveCatalog(items);
        AppLog.Info($"Сохранена рыба каталога: {item.Name}.");
        _editing = item;
        Reload();
        EditorStatus.Text = $"Сохранено: {item.Name}";
    }

    private static bool TryParseOptionalDecimal(
        string text,
        string fieldName,
        out decimal? value,
        out string error)
    {
        value = null;
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = text.Trim().Replace(
            ".",
            CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
            StringComparison.Ordinal);
        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var parsed) &&
            !decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsed))
        {
            error = $"{fieldName} должен быть числом.";
            return false;
        }

        if (parsed < 0)
        {
            error = $"{fieldName} не может быть отрицательным.";
            return false;
        }

        value = parsed;
        return true;
    }

    private void LoadRateBandFields(
        IReadOnlyList<FishRateBand> bands)
    {
        SetRateBandFields(
            FindRateBand(bands, "Незачётная", 0),
            NonQualifyingKgBox,
            NonQualifyingPriceBox,
            NonQualifyingExperienceBox);
        SetRateBandFields(
            FindRateBand(bands, "Зачётная", 1),
            QualifyingKgBox,
            QualifyingPriceBox,
            QualifyingExperienceBox);
        SetRateBandFields(
            FindRateBand(bands, "Трофей", 2),
            TrophyKgBox,
            TrophyPriceBox,
            TrophyExperienceBox);
        SetRateBandFields(
            FindRateBand(bands, "Синий трофей", 3),
            BlueTrophyKgBox,
            BlueTrophyPriceBox,
            BlueTrophyExperienceBox);
    }

    private void ClearRateBandFields()
    {
        foreach (var box in new[]
        {
            NonQualifyingKgBox,
            NonQualifyingPriceBox,
            NonQualifyingExperienceBox,
            QualifyingKgBox,
            QualifyingPriceBox,
            QualifyingExperienceBox,
            TrophyKgBox,
            TrophyPriceBox,
            TrophyExperienceBox,
            BlueTrophyKgBox,
            BlueTrophyPriceBox,
            BlueTrophyExperienceBox
        })
        {
            box.Clear();
        }
    }

    private static FishRateBand? FindRateBand(
        IReadOnlyList<FishRateBand> bands,
        string name,
        int index)
    {
        return bands.FirstOrDefault(band =>
                   band.Name.Equals(
                       name,
                       StringComparison.OrdinalIgnoreCase)) ??
               (bands.Count > index ? bands[index] : null);
    }

    private static void SetRateBandFields(
        FishRateBand? band,
        System.Windows.Controls.TextBox kilogramsBox,
        System.Windows.Controls.TextBox priceBox,
        System.Windows.Controls.TextBox experienceBox)
    {
        kilogramsBox.Text = FormatRange(
            band?.MinKilograms,
            band?.MaxKilograms,
            "0.###");
        priceBox.Text = FormatRange(
            band?.MinPrice,
            band?.MaxPrice,
            "0.##");
        experienceBox.Text = FormatRange(
            band?.MinExperience,
            band?.MaxExperience,
            "0.##");
    }

    private static string FormatRange(
        decimal? min,
        decimal? max,
        string format)
    {
        if (!min.HasValue && !max.HasValue)
        {
            return "";
        }

        if (min == max || !max.HasValue)
        {
            return (min ?? max)!.Value.ToString(
                format,
                CultureInfo.CurrentCulture);
        }

        return $"{min?.ToString(format, CultureInfo.CurrentCulture)} – " +
               $"{max?.ToString(format, CultureInfo.CurrentCulture)}";
    }

    private bool TryReadRateBands(
        out List<FishRateBand> bands,
        out string error)
    {
        bands = [];
        error = "";

        if (!TryReadRateBand(
                "Незачётная",
                NonQualifyingKgBox,
                NonQualifyingPriceBox,
                NonQualifyingExperienceBox,
                out var nonQualifying,
                out error) ||
            !TryReadRateBand(
                "Зачётная",
                QualifyingKgBox,
                QualifyingPriceBox,
                QualifyingExperienceBox,
                out var qualifying,
                out error) ||
            !TryReadRateBand(
                "Трофей",
                TrophyKgBox,
                TrophyPriceBox,
                TrophyExperienceBox,
                out var trophy,
                out error) ||
            !TryReadRateBand(
                "Синий трофей",
                BlueTrophyKgBox,
                BlueTrophyPriceBox,
                BlueTrophyExperienceBox,
                out var blueTrophy,
                out error))
        {
            return false;
        }

        bands.Add(nonQualifying);
        bands.Add(qualifying);
        bands.Add(trophy);
        bands.Add(blueTrophy);
        return true;
    }

    private static bool TryReadRateBand(
        string name,
        System.Windows.Controls.TextBox kilogramsBox,
        System.Windows.Controls.TextBox priceBox,
        System.Windows.Controls.TextBox experienceBox,
        out FishRateBand band,
        out string error)
    {
        band = new FishRateBand { Name = name };
        error = "";
        if (!TryParseRange(
                kilogramsBox.Text,
                $"{name}: кг",
                out var minKilograms,
                out var maxKilograms,
                out error) ||
            !TryParseRange(
                priceBox.Text,
                $"{name}: цена",
                out var minPrice,
                out var maxPrice,
                out error) ||
            !TryParseRange(
                experienceBox.Text,
                $"{name}: опыт",
                out var minExperience,
                out var maxExperience,
                out error))
        {
            return false;
        }

        band.MinKilograms = minKilograms;
        band.MaxKilograms = maxKilograms;
        band.MinPrice = minPrice;
        band.MaxPrice = maxPrice;
        band.MinExperience = minExperience;
        band.MaxExperience = maxExperience;
        return true;
    }

    private static bool TryParseRange(
        string text,
        string fieldName,
        out decimal? min,
        out decimal? max,
        out string error)
    {
        min = null;
        max = null;
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var parts = text.Trim().Split(
            ['–', '-'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2)
        {
            error = $"{fieldName}: укажите одно число или диапазон через «–».";
            return false;
        }

        if (!TryParseOptionalDecimal(
                parts[0],
                fieldName,
                out min,
                out error) ||
            !TryParseOptionalDecimal(
                parts.Length == 2 ? parts[1] : parts[0],
                fieldName,
                out max,
                out error))
        {
            return false;
        }

        if (min.HasValue && max.HasValue && min > max)
        {
            error = $"{fieldName}: начало диапазона больше конца.";
            return false;
        }

        return true;
    }

    private void DeleteClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_editing is null)
        {
            EditorStatus.Text = "Сначала выберите запись.";
            return;
        }

        if (System.Windows.MessageBox.Show(
                this,
                $"Удалить «{_editing.Name}»?",
                "Каталог рыбы",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) !=
            System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        _store.SaveCatalog(_catalog.Where(item => item.Id != _editing.Id));
        AppLog.Info($"Удалена рыба каталога: {_editing.Name}.");
        NewClick(sender, e);
        Reload();
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
