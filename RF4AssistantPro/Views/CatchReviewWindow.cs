using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RF4AssistantPro.Ocr;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using DockPanel = System.Windows.Controls.DockPanel;
using Dock = System.Windows.Controls.Dock;
using Grid = System.Windows.Controls.Grid;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using RowDefinition = System.Windows.Controls.RowDefinition;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using MediaColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace RF4AssistantPro.Views;

public sealed class CatchReviewWindow : Window
{
    private readonly TextBox _fishName = new();
    private readonly TextBox _weight = new();
    private readonly ComboBox _weightUnit = new();
    private readonly TextBox _length = new();
    private readonly CheckBox _quality = new();
    private readonly string _saveButtonText;

    public CatchReviewWindow(
        string screenshotPath,
        RecognizedCatch? recognized,
        string? titleOverride = null,
        string saveButtonText = "Сохранить улов")
    {
        _saveButtonText = saveButtonText;
        Title = titleOverride ?? (recognized is null
            ? "Проверка нераспознанного улова"
            : "Проверка результата OCR");
        Width = 760;
        Height = 680;
        MinWidth = 620;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(MediaColor.FromRgb(7, 17, 15));
        Foreground = new SolidColorBrush(MediaColor.FromRgb(240, 242, 232));

        _weightUnit.Items.Add("г");
        _weightUnit.Items.Add("кг");
        _weightUnit.SelectedIndex = 0;
        _quality.Content = "Зачётная";
        _quality.Foreground = new SolidColorBrush(MediaColor.FromRgb(240, 242, 232));

        if (recognized is not null)
        {
            _fishName.Text = recognized.FishName;
            if (recognized.WeightKg < 1m)
            {
                _weight.Text = (recognized.WeightKg * 1000m)
                    .ToString("0.##", CultureInfo.InvariantCulture);
                _weightUnit.SelectedItem = "г";
            }
            else
            {
                _weight.Text = recognized.WeightKg
                    .ToString("0.###", CultureInfo.InvariantCulture);
                _weightUnit.SelectedItem = "кг";
            }

            _length.Text = recognized.LengthCm?
                .ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            _quality.IsChecked = recognized.Quality.Contains(
                "зач",
                StringComparison.OrdinalIgnoreCase);
        }

        Content = BuildContent(screenshotPath);
    }

    public RecognizedCatch? Result { get; private set; }

    private FrameworkElement BuildContent(string screenshotPath)
    {
        var root = new Grid
        {
            Margin = new Thickness(18)
        };
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 16)
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

        var fields = new Grid();
        for (var index = 0; index < 4; index++)
        {
            fields.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = index % 2 == 0
                    ? GridLength.Auto
                    : new GridLength(1, GridUnitType.Star)
            });
        }

        AddField(fields, 0, "Рыба", _fishName);

        var weightPanel = new DockPanel();
        _weightUnit.Width = 70;
        _weightUnit.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(_weightUnit, Dock.Right);
        weightPanel.Children.Add(_weightUnit);
        weightPanel.Children.Add(_weight);
        AddField(fields, 1, "Вес", weightPanel);

        AddField(fields, 2, "Длина, см", _length);
        AddField(fields, 3, "Статус", _quality);
        Grid.SetRow(fields, 1);
        root.Children.Add(fields);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button
        {
            Content = "Отмена",
            MinWidth = 110,
            Height = 38,
            IsCancel = true
        };
        var save = new Button
        {
            Content = _saveButtonText,
            MinWidth = 140,
            Height = 38,
            Margin = new Thickness(8, 0, 0, 0)
        };
        save.Click += SaveClick;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        return root;
    }

    private static void AddField(
        Grid grid,
        int fieldIndex,
        string label,
        UIElement editor)
    {
        var row = fieldIndex / 2;
        while (grid.RowDefinitions.Count <= row)
        {
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });
        }

        var column = fieldIndex % 2 * 2;
        var text = new TextBlock
        {
            Text = label,
            Margin = new Thickness(
                column == 0 ? 0 : 18,
                8,
                8,
                8),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(text, row);
        Grid.SetColumn(text, column);
        grid.Children.Add(text);

        if (editor is Control control)
        {
            control.MinHeight = 32;
            control.VerticalContentAlignment =
                VerticalAlignment.Center;
        }

        Grid.SetRow(editor, row);
        Grid.SetColumn(editor, column + 1);
        grid.Children.Add(editor);
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var name = _fishName.Text.Trim();
        if (name.Length < 2)
        {
            ShowValidation("Введите название рыбы.");
            return;
        }

        if (!decimal.TryParse(
                _weight.Text.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var displayedWeight) ||
            displayedWeight <= 0m)
        {
            ShowValidation("Введите корректный вес.");
            return;
        }

        var weightKg = Equals(_weightUnit.SelectedItem, "кг")
            ? displayedWeight
            : displayedWeight / 1000m;
        decimal? length = null;
        if (!string.IsNullOrWhiteSpace(_length.Text))
        {
            if (!decimal.TryParse(
                    _length.Text.Trim().Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var parsedLength) ||
                parsedLength <= 0m)
            {
                ShowValidation("Введите корректную длину или оставьте поле пустым.");
                return;
            }

            length = parsedLength;
        }

        Result = new RecognizedCatch
        {
            FishName = name,
            WeightKg = weightKg,
            LengthCm = length,
            Quality = _quality.IsChecked == true
                ? "Зачётная"
                : ""
        };
        DialogResult = true;
    }

    private void ShowValidation(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Проверка улова",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}