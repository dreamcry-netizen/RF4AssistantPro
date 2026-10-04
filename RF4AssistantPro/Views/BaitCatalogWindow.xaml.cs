using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using RF4AssistantPro.Baits;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Views;

public partial class BaitCatalogWindow : System.Windows.Window
{
    private static readonly string[] Categories =
    [
        "Натуральные наживки",
        "Карповые приманки",
        "Дополнительно"
    ];

    private static readonly string[] Subcategories =
    [
        "Черви", "Личинки", "Насекомые", "Ракообразные",
        "Каши и тесто", "Натуральные", "Живые", "Живцы",
        "Тонущие бойлы", "Pop-Up бойлы", "Пеллетс",
        "Искусственная кукуруза", "Зиг-риг пенки",
        "Дипы и ароматизаторы"
    ];

    private readonly BaitCatalogStore _store;
    private readonly ObservableCollection<BaitCatalogItem> _catalog = [];
    private readonly ObservableCollection<UnrecognizedBait> _unrecognized = [];
    private BaitCatalogItem? _editing;
    private string _selectedImagePath = "";

    public BaitCatalogWindow(
        BaitCatalogStore store,
        bool openEditor = false,
        string? editItemId = null)
    {
        _store = store;
        InitializeComponent();
        CategoryBox.ItemsSource = Categories;
        SubcategoryBox.ItemsSource = Subcategories;
        CatalogGrid.ItemsSource = _catalog;
        UnrecognizedGrid.ItemsSource = _unrecognized;
        Reload();

        if (openEditor)
        {
            NewClick(this, new System.Windows.RoutedEventArgs());
            EditorTab.IsSelected = true;
        }
        else if (!string.IsNullOrWhiteSpace(editItemId))
        {
            var item = _catalog.FirstOrDefault(value =>
                string.Equals(
                    value.Id,
                    editItemId,
                    StringComparison.Ordinal));
            if (item is not null)
            {
                CatalogGrid.SelectedItem = item;
                CatalogGrid.ScrollIntoView(item);
                LoadEditor(item);
                EditorTab.IsSelected = true;
            }
        }
    }

    public string CatalogCountText => $"Записей: {_catalog.Count}";

    private void Reload()
    {
        // После сохранения JSON загружается заново и создаёт новые экземпляры
        // записей. Запоминаем Id, чтобы редактор не продолжал изменять старый
        // объект, которого больше нет в коллекции.
        var editingId = _editing?.Id;

        _catalog.Clear();
        foreach (var item in _store.LoadCatalog())
        {
            _catalog.Add(item);
        }

        _unrecognized.Clear();
        foreach (var item in _store.LoadUnrecognized())
        {
            _unrecognized.Add(item);
        }

        DataContext = null;
        DataContext = this;
        ApplySearch();

        if (!string.IsNullOrWhiteSpace(editingId))
        {
            var reloadedItem = _catalog.FirstOrDefault(
                item => string.Equals(
                    item.Id,
                    editingId,
                    StringComparison.Ordinal));
            if (reloadedItem is not null)
            {
                LoadEditor(reloadedItem);
            }
            else
            {
                _editing = null;
            }
        }
    }

    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private void ApplySearch()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        CatalogGrid.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _catalog
            : _catalog.Where(item =>
                $"{item.DisplayName} {item.CategoryDisplay} {item.Aliases}"
                    .Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
    }

    private void CatalogSelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is not BaitCatalogItem item)
        {
            return;
        }

        LoadEditor(item);
    }

    private void LoadEditor(BaitCatalogItem item)
    {
        _editing = item;
        NameBox.Text = item.Name;
        BrandBox.Text = item.Brand;
        CategoryBox.Text = item.Category;
        SubcategoryBox.Text = item.Subcategory;
        AliasesBox.Text = item.Aliases;
        EnabledBox.IsChecked = item.IsEnabled;
        _selectedImagePath = item.ImagePath;
        PreviewImage.Source = LoadImage(item.ImagePath);
        EditorStatus.Text = $"Редактирование: {item.DisplayName}";
    }

    private void NewClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _editing = null;
        NameBox.Clear();
        BrandBox.Clear();
        CategoryBox.Text = "";
        SubcategoryBox.Text = "";
        AliasesBox.Clear();
        EnabledBox.IsChecked = true;
        _selectedImagePath = "";
        PreviewImage.Source = null;
        EditorStatus.Text = "Новая запись.";
    }

    private void ChooseImageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Все файлы|*.*",
            Title = "Изображение наживки"
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
            EditorStatus.Text = "Укажите название наживки.";
            return;
        }

        var item = _editing ?? new BaitCatalogItem();
        item.Name = name;
        item.Brand = BrandBox.Text.Trim();
        item.Category = CategoryBox.Text.Trim();
        item.Subcategory = SubcategoryBox.Text.Trim();
        item.Aliases = AliasesBox.Text.Trim();
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
        AppLog.Info($"Сохранена наживка каталога: {item.DisplayName}.");
        _editing = item;
        Reload();
        EditorStatus.Text = $"Сохранено: {item.DisplayName}";
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
                $"Удалить «{_editing.DisplayName}»?",
                "Каталог наживок",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) !=
            System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        _store.SaveCatalog(_catalog.Where(item => item.Id != _editing.Id));
        AppLog.Info($"Удалена наживка каталога: {_editing.DisplayName}.");
        NewClick(sender, e);
        Reload();
    }

    private void CreateFromUnrecognizedClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (UnrecognizedGrid.SelectedItem is not UnrecognizedBait item)
        {
            return;
        }

        NewClick(sender, e);
        NameBox.Text = item.CandidateName;
        AliasesBox.Text = item.CandidateName;
        EditorTab.IsSelected = true;
        EditorStatus.Text =
            "Заполните категорию, выберите изображение и сохраните запись.";
    }

    private void DeleteUnrecognizedClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (UnrecognizedGrid.SelectedItem is not UnrecognizedBait item)
        {
            return;
        }

        _store.SaveUnrecognized(_unrecognized.Where(value => value.Id != item.Id));
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
        image.CacheOption =
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}