using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RF4AssistantPro.Services;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;

namespace RF4AssistantPro.Views;

public sealed class ScreenshotStorageSettingsWindow : Window
{
    private readonly WpfTextBox _maxAgeDays = new();
    private readonly WpfTextBox _maxTotalMegabytes = new();
    private readonly WpfCheckBox _autoCleanup = new();
    private readonly TextBlock _summaryText = new();
    private readonly Action<ScreenshotRetentionSettings> _save;
    private readonly Func<ScreenshotRetentionSettings, ScreenshotCleanupResult>
        _cleanup;
    private ScreenshotStorageSummary _summary;

    public ScreenshotStorageSettingsWindow(
        ScreenshotRetentionSettings settings,
        ScreenshotStorageSummary summary,
        Action<ScreenshotRetentionSettings> save,
        Func<ScreenshotRetentionSettings, ScreenshotCleanupResult> cleanup)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(cleanup);

        _save = save;
        _cleanup = cleanup;
        _summary = summary;
        Title = "Хранение снимков";
        Width = 600;
        Height = 500;
        MinWidth = 520;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(7, 17, 15));
        Foreground = new SolidColorBrush(Color.FromRgb(240, 242, 232));

        _maxAgeDays.Text = settings.MaxAgeDays.ToString(
            CultureInfo.InvariantCulture);
        _maxTotalMegabytes.Text = settings.MaxTotalMegabytes.ToString(
            CultureInfo.InvariantCulture);
        _autoCleanup.Content = "Запускать автоочистку после новых снимков";
        _autoCleanup.IsChecked = settings.AutoCleanupEnabled;
        _summaryText.Foreground = new SolidColorBrush(
            Color.FromRgb(168, 183, 169));
        _summaryText.TextWrapping = TextWrapping.Wrap;
        UpdateSummaryText();
        Content = BuildContent();
    }

    private FrameworkElement BuildContent()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(22)
        };
        root.Children.Add(new TextBlock
        {
            Text = "Управление хранением снимков",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = "Удаляются только PNG без ссылок из уловов, садка, кафе " +
                   "и очереди проверки. Значение 0 отключает ограничение.",
            Margin = new Thickness(0, 8, 0, 18),
            Foreground = new SolidColorBrush(Color.FromRgb(168, 183, 169)),
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new TextBlock
        {
            Text = "Текущее состояние",
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(_summaryText);

        root.Children.Add(CreateField(
            "Максимальный возраст, дней",
            _maxAgeDays,
            "0 — не ограничивать возрастом"));
        root.Children.Add(CreateField(
            "Максимальный объём, МБ",
            _maxTotalMegabytes,
            "0 — не ограничивать объёмом"));
        _autoCleanup.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(_autoCleanup);

        var notice = new TextBlock
        {
            Text = "Очистка сначала удаляет самые старые незащищённые " +
                   "снимки. Ссылочные файлы не удаляются.",
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(242, 194, 102)),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(notice);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 22, 0, 0)
        };
        var cleanup = new Button
        {
            Content = "Очистить сейчас",
            MinWidth = 135,
            Height = 36
        };
        cleanup.Click += CleanupClick;
        var cancel = new Button
        {
            Content = "Отмена",
            MinWidth = 100,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true
        };
        var save = new Button
        {
            Content = "Сохранить",
            MinWidth = 110,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true
        };
        save.Click += SaveClick;
        buttons.Children.Add(cleanup);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        root.Children.Add(buttons);
        return root;
    }

    private static FrameworkElement CreateField(
        string label,
        WpfTextBox editor,
        string hint)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 16, 0, 0)
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold
        });
        editor.Width = 150;
        editor.Height = 32;
        editor.Margin = new Thickness(0, 5, 0, 0);
        panel.Children.Add(editor);
        panel.Children.Add(new TextBlock
        {
            Text = hint,
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(168, 183, 169))
        });
        return panel;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        try
        {
            _save(settings);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ShowError($"Не удалось сохранить настройки: {exception.Message}");
        }
    }

    private void CleanupClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Удалить старые и превышающие лимит незащищённые снимки?\n" +
            "Файлы, связанные с записями, останутся.",
            "Очистка снимков",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = _cleanup(settings);
            _summary = new ScreenshotStorageSummary(
                Math.Max(0, _summary.FileCount - result.DeletedFileCount),
                Math.Max(0, _summary.TotalBytes - result.FreedBytes),
                _summary.ProtectedFileCount,
                _summary.ProtectedBytes);
            UpdateSummaryText();
            MessageBox.Show(
                this,
                result.DisplayText,
                "Очистка снимков",
                MessageBoxButton.OK,
                result.Errors.Count == 0
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            ShowError($"Не удалось очистить снимки: {exception.Message}");
        }
    }

    private bool TryReadSettings(out ScreenshotRetentionSettings settings)
    {
        settings = new ScreenshotRetentionSettings();
        if (!int.TryParse(
                _maxAgeDays.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var ageDays) || ageDays < 0)
        {
            ShowError("Возраст должен быть целым числом от 0 до 3650.");
            return false;
        }

        if (!long.TryParse(
                _maxTotalMegabytes.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var megabytes) || megabytes < 0)
        {
            ShowError("Объём должен быть целым числом от 0 до 1048576.");
            return false;
        }

        settings = new ScreenshotRetentionSettings
        {
            MaxAgeDays = ageDays,
            MaxTotalMegabytes = megabytes,
            AutoCleanupEnabled = _autoCleanup.IsChecked == true
        }.Normalize();
        return true;
    }

    private void UpdateSummaryText()
    {
        _summaryText.Text = _summary.DisplayText;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Хранение снимков",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
