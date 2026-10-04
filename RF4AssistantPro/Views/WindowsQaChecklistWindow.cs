using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RF4AssistantPro.Services;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace RF4AssistantPro.Views;

public sealed class WindowsQaChecklistWindow : Window
{
    private readonly WindowsQaChecklistStore _store;
    private readonly StackPanel _itemsPanel = new();
    private readonly TextBlock _summaryText = new();
    private readonly Dictionary<string, TextBox> _notes = new(
        StringComparer.OrdinalIgnoreCase);
    private WindowsQaChecklistState _state;

    public WindowsQaChecklistWindow(WindowsQaChecklistStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _state = _store.Load();

        Title = "Windows QA — ручной чек-лист";
        Width = 920;
        Height = 780;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(14, 17, 21));
        Foreground = new SolidColorBrush(Color.FromRgb(240, 242, 232));

        Content = BuildContent();
        RenderItems();
    }

    private FrameworkElement BuildContent()
    {
        var root = new DockPanel
        {
            Margin = new Thickness(22)
        };

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 14)
        };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = "Windows QA",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Временный ручной чек-лист. Каждый результат сохраняется " +
                   "в JSON и WindowsQa.log.",
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(168, 183, 169)),
            TextWrapping = TextWrapping.Wrap
        });
        _summaryText.Margin = new Thickness(0, 10, 0, 0);
        _summaryText.FontWeight = FontWeights.SemiBold;
        header.Children.Add(_summaryText);
        root.Children.Add(header);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var openLog = new Button
        {
            Content = "Открыть папку логов",
            MinWidth = 150,
            Height = 34
        };
        openLog.Click += OpenLogClick;
        var reset = new Button
        {
            Content = "Сбросить",
            MinWidth = 100,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0)
        };
        reset.Click += ResetClick;
        var close = new Button
        {
            Content = "Закрыть",
            MinWidth = 100,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true
        };
        footer.Children.Add(openLog);
        footer.Children.Add(reset);
        footer.Children.Add(close);
        root.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _itemsPanel
        };
        root.Children.Add(scroll);
        return root;
    }

    private void RenderItems()
    {
        _itemsPanel.Children.Clear();
        _notes.Clear();
        _summaryText.Text =
            $"Пройдено: {_state.PassedCount}/{_state.Items.Count}; " +
            $"не пройдено: {_state.FailedCount}; " +
            $"ожидают: {_state.PendingCount}; " +
            $"ReleaseReady: {_state.ReleaseReady}";
        _summaryText.Foreground = new SolidColorBrush(
            _state.ReleaseReady
                ? Color.FromRgb(114, 208, 161)
                : Color.FromRgb(234, 194, 107));

        foreach (var item in _state.Items)
        {
            _itemsPanel.Children.Add(BuildItem(item));
        }
    }

    private FrameworkElement BuildItem(WindowsQaCheckResult item)
    {
        var border = new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(21, 26, 32)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(43, 54, 66)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        var details = new StackPanel();
        details.Children.Add(new TextBlock
        {
            Text = $"{item.Id}  ·  {item.Title}",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var statusText = new TextBlock
        {
            Text = FormatStatus(item),
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = StatusBrush(item.Status)
        };
        details.Children.Add(statusText);
        var note = new TextBox
        {
            Text = item.Note,
            Height = 28,
            Margin = new Thickness(0, 7, 0, 0),
            ToolTip = "Необязательный комментарий или причина сбоя"
        };
        _notes[item.Id] = note;
        details.Children.Add(note);
        grid.Children.Add(details);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        actions.Children.Add(CreateActionButton(
            "Успешно",
            item.Id,
            WindowsQaCheckStatus.Passed,
            Color.FromRgb(36, 98, 70)));
        actions.Children.Add(CreateActionButton(
            "Сбой",
            item.Id,
            WindowsQaCheckStatus.Failed,
            Color.FromRgb(110, 53, 53)));
        actions.Children.Add(CreateActionButton(
            "Ожидает",
            item.Id,
            WindowsQaCheckStatus.Pending,
            Color.FromRgb(70, 68, 44)));
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        border.Child = grid;
        return border;
    }

    private Button CreateActionButton(
        string content,
        string id,
        WindowsQaCheckStatus status,
        Color color)
    {
        var button = new Button
        {
            Content = content,
            Tag = new QaAction(id, status),
            MinWidth = 78,
            Height = 30,
            Margin = new Thickness(5, 0, 0, 0),
            Background = new SolidColorBrush(color)
        };
        button.Click += SetStatusClick;
        return button;
    }

    private void SetStatusClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: QaAction action })
        {
            return;
        }

        _state = _store.SetStatus(
            action.Id,
            action.Status,
            _notes.TryGetValue(action.Id, out var note)
                ? note.Text
                : null);
        RenderItems();
    }

    private void ResetClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Сбросить все результаты Windows QA?",
            "Windows QA",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        _state = _store.Reset();
        RenderItems();
    }

    private void OpenLogClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_store.LogPath)!);
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetDirectoryName(_store.LogPath)!,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Windows QA",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string FormatStatus(WindowsQaCheckResult item)
    {
        var status = item.Status switch
        {
            WindowsQaCheckStatus.Passed => "УСПЕШНО",
            WindowsQaCheckStatus.Failed => "СБОЙ",
            _ => "ОЖИДАЕТ"
        };
        return item.UpdatedAt is { } updatedAt
            ? $"{status} · {updatedAt:dd.MM.yyyy HH:mm}" +
              (string.IsNullOrWhiteSpace(item.Note)
                  ? ""
                  : $" · {item.Note}")
            : status;
    }

    private static Brush StatusBrush(WindowsQaCheckStatus status)
    {
        return new SolidColorBrush(status switch
        {
            WindowsQaCheckStatus.Passed => Color.FromRgb(114, 208, 161),
            WindowsQaCheckStatus.Failed => Color.FromRgb(238, 126, 126),
            _ => Color.FromRgb(234, 194, 107)
        });
    }

    private sealed record QaAction(
        string Id,
        WindowsQaCheckStatus Status);
}