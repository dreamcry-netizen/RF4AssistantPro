using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RF4AssistantPro.Services;
using Button = System.Windows.Controls.Button;
using Binding = System.Windows.Data.Binding;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using DataGrid = System.Windows.Controls.DataGrid;
using Grid = System.Windows.Controls.Grid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;

namespace RF4AssistantPro.Views;

public sealed class KeepnetOcrAliasWindow : Window
{
    private readonly KeepnetOcrAliasStore _store;
    private readonly ObservableCollection<KeepnetOcrAliasRow> _rows = [];
    private readonly DataGrid _grid = new();

    public KeepnetOcrAliasWindow(KeepnetOcrAliasStore? store = null)
    {
        _store = store ?? new KeepnetOcrAliasStore();
        Title = "OCR-словарь садка";
        Width = 760;
        Height = 520;
        MinWidth = 600;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(8, 12, 16));
        Foreground = Brushes.White;

        foreach (var item in _store.Load().OrderBy(item => item.Key))
        {
            _rows.Add(new KeepnetOcrAliasRow
            {
                OcrText = item.Key,
                ConfirmedName = item.Value
            });
        }

        Content = BuildContent();
    }

    private FrameworkElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Обученные названия OCR садка",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 5, 0, 12),
            Text =
                "Здесь можно добавить, исправить или удалить ошибочно " +
                "сохранённый псевдоним рыбы.",
            Foreground = new SolidColorBrush(Color.FromRgb(152, 163, 179)),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(header);

        _grid.AutoGenerateColumns = false;
        _grid.CanUserAddRows = true;
        _grid.CanUserDeleteRows = true;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.ItemsSource = _rows;
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Результат OCR",
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(KeepnetOcrAliasRow.OcrText))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Подтверждённое название",
            Width = new DataGridLength(1.35, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(KeepnetOcrAliasRow.ConfirmedName))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var clear = new Button
        {
            Content = "Очистить словарь",
            MinWidth = 135,
            Height = 34
        };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    this,
                    "Удалить все обученные OCR-псевдонимы садка?",
                    "OCR-словарь садка",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _rows.Clear();
            }
        };
        footer.Children.Add(clear);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Отмена",
            MinWidth = 95,
            Height = 34,
            IsCancel = true
        };
        var save = new Button
        {
            Content = "Сохранить",
            MinWidth = 110,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true
        };
        save.Click += SaveClick;
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        var aliases = _rows
            .Where(row =>
                !string.IsNullOrWhiteSpace(row.OcrText) &&
                !string.IsNullOrWhiteSpace(row.ConfirmedName))
            .GroupBy(
                row => row.OcrText.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().ConfirmedName.Trim(),
                StringComparer.OrdinalIgnoreCase);
        _store.Replace(aliases);
        DialogResult = true;
    }
}

public sealed class KeepnetOcrAliasRow
{
    public string OcrText { get; set; } = "";

    public string ConfirmedName { get; set; } = "";
}