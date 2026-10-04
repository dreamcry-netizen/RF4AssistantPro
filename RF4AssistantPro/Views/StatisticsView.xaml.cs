using RF4AssistantPro;
using RF4AssistantPro.Services;
using RF4AssistantPro.ViewModels;
using System.Collections.Specialized;

namespace RF4AssistantPro.Views;

public partial class StatisticsView : System.Windows.Controls.UserControl
{
    private System.Windows.Controls.Button? _activeNavigationButton;
    private bool _baitSortAscending = true;
    private bool _fishSortAscending = true;
    private bool _sidebarCollapsed;

    public StatisticsView()
    {
        InitializeComponent();
        _activeNavigationButton = OverviewButton;
        Loaded += StatisticsViewLoaded;
    }

    private void StatisticsViewLoaded(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Loaded -= StatisticsViewLoaded;

        // После Loaded созданы DataGrid и их визуальные элементы. Ранний
        // вызов из конструктора не всегда находил таблицу, поэтому в ней
        // оставался старый формат «0,08 кг».
        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.CafeSnapshots.CollectionChanged +=
                CafeSnapshotsCollectionChanged;
        }

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                ApplyDisplayAdjustments();
                AddDeleteButtonsToCafeSnapshots();
                AddCafeProgressIndicators();
                UpdateCafeSetupStep();
            }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void CafeSnapshotsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                AddDeleteButtonsToCafeSnapshots();
                AddCafeProgressIndicators();
                UpdateCafeSetupStep();
            }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateCafeSetupStep()
    {
        if (DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        var textBlocks = GetVisualDescendants(this)
            .OfType<System.Windows.Controls.TextBlock>()
            .ToList();
        var title = textBlocks.FirstOrDefault(textBlock =>
            textBlock.Text is "Проверьте кафе" or "Кафе проверено");
        if (title?.Parent is not System.Windows.Controls.StackPanel details ||
            details.Parent is not System.Windows.Controls.StackPanel row)
        {
            return;
        }

        var subtitle = details.Children
            .OfType<System.Windows.Controls.TextBlock>()
            .FirstOrDefault(textBlock => !ReferenceEquals(textBlock, title));
        var badge = row.Children
            .OfType<System.Windows.Controls.Border>()
            .FirstOrDefault();
        var marker = badge?.Child as System.Windows.Controls.TextBlock;
        var completed = viewModel.CafeSnapshots.Count > 0;

        title.Text = completed ? "Кафе проверено" : "Проверьте кафе";
        if (subtitle is not null)
        {
            subtitle.Text = completed
                ? "Предложения загружены"
                : "Снимок даст предложения";
        }

        if (badge is not null)
        {
            badge.Background = new System.Windows.Media.SolidColorBrush(
                completed
                    ? System.Windows.Media.Color.FromRgb(36, 98, 70)
                    : System.Windows.Media.Color.FromRgb(39, 68, 91));
        }

        if (marker is not null)
        {
            marker.Text = completed ? "✓" : "3";
            marker.Foreground = new System.Windows.Media.SolidColorBrush(
                completed
                    ? System.Windows.Media.Color.FromRgb(165, 235, 197)
                    : System.Windows.Media.Color.FromRgb(168, 217, 255));
        }

        var progress = textBlocks.FirstOrDefault(textBlock =>
            textBlock.Text is "3 шага" or "3/3");
        if (progress is not null)
        {
            progress.Text = completed ? "3/3" : "3 шага";
        }
    }

    private void AddDeleteButtonsToCafeSnapshots()
    {
        UpdateLayout();

        foreach (var border in GetVisualDescendants(this)
                     .OfType<System.Windows.Controls.Border>())
        {
            if (border.DataContext is not CafeSnapshotItemViewModel snapshot ||
                border.Child is not System.Windows.Controls.Grid grid ||
                grid.ColumnDefinitions.Count != 3)
            {
                continue;
            }

            grid.ColumnDefinitions.Add(
                new System.Windows.Controls.ColumnDefinition
                {
                    Width = System.Windows.GridLength.Auto
                });

            var deleteButton = new System.Windows.Controls.Button
            {
                Width = 30,
                Height = 30,
                Margin = new System.Windows.Thickness(10, 5, 0, 0),
                Padding = new System.Windows.Thickness(0),
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Content = "✕",
                ToolTip = "Удалить снимок",
                Tag = snapshot
            };
            deleteButton.Click += DeleteCafeSnapshotClick;
            System.Windows.Controls.Grid.SetColumn(deleteButton, 3);
            grid.Children.Add(deleteButton);
        }
    }

    private void AddCafeProgressIndicators()
    {
        UpdateLayout();

        foreach (var border in GetVisualDescendants(this)
                     .OfType<System.Windows.Controls.Border>())
        {
            if (border.DataContext is not CafeSnapshotItemViewModel snapshot ||
                border.Child is not System.Windows.Controls.Grid grid)
            {
                continue;
            }

            var details = grid.Children
                .OfType<System.Windows.Controls.StackPanel>()
                .FirstOrDefault(panel =>
                    System.Windows.Controls.Grid.GetColumn(panel) == 1);
            if (details is null)
            {
                continue;
            }

            var progressText = details.Children
                .OfType<System.Windows.Controls.TextBlock>()
                .FirstOrDefault(textBlock =>
                    Equals(textBlock.Tag, "CafeOrderProgress"));
            if (progressText is null)
            {
                progressText = new System.Windows.Controls.TextBlock
                {
                    Tag = "CafeOrderProgress",
                    Margin = new System.Windows.Thickness(0, 3, 0, 0),
                    FontSize = 11,
                    FontWeight = System.Windows.FontWeights.SemiBold
                };
                details.Children.Add(progressText);
            }

            progressText.Text = snapshot.ProgressText;
            progressText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    snapshot.IsCompleted
                        ? System.Windows.Media.Color.FromRgb(114, 208, 161)
                        : System.Windows.Media.Color.FromRgb(184, 222, 246));
        }
    }

    private void DeleteCafeSnapshotClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;

        var snapshot = sender switch
        {
            System.Windows.Controls.Button
            {
                Tag: CafeSnapshotItemViewModel taggedSnapshot
            } => taggedSnapshot,
            System.Windows.FrameworkElement
            {
                DataContext: CafeSnapshotItemViewModel dataSnapshot
            } => dataSnapshot,
            _ => null
        };

        if (snapshot is not null &&
            DataContext is StatisticsViewModel viewModel)
        {
            viewModel.DeleteCafeSnapshot(snapshot);
        }
    }

    private void ApplyDisplayAdjustments()
    {
        AddRecognitionButtons();
        AddLogsButton();

        foreach (var element in GetVisualDescendants(this))
        {
            if (element is System.Windows.Controls.TextBlock textBlock)
            {
                if (textBlock.Text.StartsWith(
                        "PRO ·",
                        StringComparison.OrdinalIgnoreCase))
                {
                    textBlock.Text = AppVersion.Label;
                }

                var binding = System.Windows.Data.BindingOperations.GetBinding(
                    textBlock,
                    System.Windows.Controls.TextBlock.TextProperty);
                var path = binding?.Path?.Path;

                if (path == "Summary.BestWeightKg")
                {
                    textBlock.SetBinding(
                        System.Windows.Controls.TextBlock.TextProperty,
                        new System.Windows.Data.Binding(
                            "Summary.BestWeightDisplay"));
                }
                else if (path == "Summary.AverageWeightKg")
                {
                    textBlock.SetBinding(
                        System.Windows.Controls.TextBlock.TextProperty,
                        new System.Windows.Data.Binding(
                            "Summary.AverageWeightDisplay"));
                }

                if (textBlock.Text == "Обновляется после Space")
                {
                    textBlock.Margin = new System.Windows.Thickness(22, 0, 0, 0);
                    if (textBlock.Parent is System.Windows.Controls.DockPanel panel)
                    {
                        panel.LastChildFill = false;
                    }
                }
                else if (textBlock.Text == "Кафе сегодня")
                {
                    textBlock.Margin = new System.Windows.Thickness(0, 0, 22, 0);
                    if (textBlock.Parent is System.Windows.Controls.DockPanel panel)
                    {
                        panel.LastChildFill = false;
                    }
                }
            }
            else if (element is System.Windows.Controls.Button button &&
                     button.Content?.ToString() == "Скрин кафе")
            {
                button.MinWidth = 112;
                button.Padding = new System.Windows.Thickness(12, 5, 12, 5);
            }
            else if (element is System.Windows.Controls.DataGrid dataGrid)
            {
                var textColumns = dataGrid.Columns
                    .OfType<System.Windows.Controls.DataGridTextColumn>()
                    .ToList();

                if (textColumns.Any(column =>
                        column.Header?.ToString() == "Вес") &&
                    textColumns.All(column =>
                        column.Header?.ToString() != "Наживка"))
                {
                    var weightIndex = dataGrid.Columns
                        .Select((column, index) => (column, index))
                        .First(item =>
                            item.column.Header?.ToString() == "Вес")
                        .index;
                    dataGrid.Columns.Insert(
                        weightIndex,
                        new System.Windows.Controls.DataGridTextColumn
                        {
                            Header = "Наживка",
                            Width = new System.Windows.Controls.DataGridLength(120),
                            Binding = new System.Windows.Data.Binding("BaitName")
                        });
                }

                foreach (var column in dataGrid.Columns
                             .OfType<System.Windows.Controls.DataGridTextColumn>())
                {
                    var header = column.Header?.ToString();
                    if (header == "Вес")
                    {
                        column.Binding = new System.Windows.Data.Binding(
                            "WeightDisplay");
                    }
                    else if (header == "Средний вес")
                    {
                        column.Binding = new System.Windows.Data.Binding(
                            "AverageWeightDisplay");
                    }
                }
            }
        }
    }

    private void AddLogsButton()
    {
        if (GetVisualDescendants(this)
            .OfType<System.Windows.Controls.Button>()
            .Any(button => button.Content?.ToString() == "Логи"))
        {
            return;
        }

        foreach (var panel in GetVisualDescendants(this)
                     .OfType<System.Windows.Controls.StackPanel>())
        {
            var buttons = panel.Children
                .OfType<System.Windows.Controls.Button>()
                .ToList();
            if (buttons.All(button =>
                    button.Content?.ToString() != "Очистить уловы"))
            {
                continue;
            }

            var logsButton = new System.Windows.Controls.Button
            {
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                Content = "Логи",
                ToolTip = AppLog.DirectoryPath
            };
            logsButton.Click += OpenLogsClick;
            panel.Children.Add(logsButton);

            var diagnosticsButton = new System.Windows.Controls.Button
            {
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                Content = "Диагностика ZIP",
                ToolTip = "Создать обезличенный архив журналов"
            };
            diagnosticsButton.Click += ExportDiagnosticsClick;
            panel.Children.Add(diagnosticsButton);

            var csvButton = new System.Windows.Controls.Button
            {
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                Content = "Экспорт CSV",
                ToolTip =
                    "Экспортировать исходные записи и сводную аналитику"
            };
            csvButton.Click += ExportCsvClick;
            panel.Children.Add(csvButton);
            break;
        }
    }

    private void AddRecognitionButtons()
    {
        if (GetVisualDescendants(this)
            .OfType<System.Windows.Controls.Button>()
            .Any(button =>
                button.Content?.ToString()?.Contains(
                    "Определить водоём",
                    StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        foreach (var panel in GetVisualDescendants(this)
                     .OfType<System.Windows.Controls.StackPanel>())
        {
            var buttons = panel.Children
                .OfType<System.Windows.Controls.Button>()
                .ToList();
            if (buttons.All(button =>
                    button.Content?.ToString() != "◉  Сделать снимок улова") ||
                buttons.All(button =>
                    button.Content?.ToString() != "▣  Скрин кафе") ||
                panel.Parent is not System.Windows.Controls.StackPanel parent)
            {
                continue;
            }

            var recognitionPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new System.Windows.Thickness(0, 8, 0, 0)
            };

            var baitButton = new System.Windows.Controls.Button
            {
                Width = 165,
                Height = 42,
                Content = "⌖  Определить наживку",
                ToolTip =
                    "Активировать одноразовый снимок снасти по клавише V"
            };
            baitButton.Click += ArmBaitRecognitionClick;

            var waterBodyButton = new System.Windows.Controls.Button
            {
                Width = 165,
                Height = 42,
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                Content = "⌖  Определить водоём",
                ToolTip =
                    "Активировать одноразовый снимок карты по клавише M"
            };
            waterBodyButton.Click += ArmWaterBodyRecognitionClick;

            var reviewButton = new System.Windows.Controls.Button
            {
                Width = 165,
                Height = 42,
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                Content = "⌕  Проверить снимок",
                ToolTip =
                    "Повторно распознать сохранённую карточку улова"
            };
            reviewButton.Click += ReviewSavedScreenshotClick;

            recognitionPanel.Children.Add(baitButton);
            recognitionPanel.Children.Add(waterBodyButton);
            recognitionPanel.Children.Add(reviewButton);

            var panelIndex = parent.Children.IndexOf(panel);
            parent.Children.Insert(panelIndex + 1, recognitionPanel);
            break;
        }
    }

    private static IEnumerable<System.Windows.DependencyObject>
        GetVisualDescendants(System.Windows.DependencyObject parent)
    {
        var count = System.Windows.Media.VisualTreeHelper
            .GetChildrenCount(parent);

        for (var index = 0; index < count; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper
                .GetChild(parent, index);
            yield return child;

            foreach (var descendant in GetVisualDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void OverviewClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(OverviewButton, MainScrollViewer, "Обзор");
        MainScrollViewer.ScrollToHome();
    }

    private void CatchesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(CatchesButton, CatchesPage, "Уловы");
    }

    private void WaterBodiesClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        ShowPage(WaterBodiesButton, WaterBodiesPage, "Водоёмы");
    }

    private void CafeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(CafeButton, CafePage, "Кафе");
    }

    private void KeepnetClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(KeepnetButton, KeepnetPage, "Садок");
    }

    private void BaitsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(BaitsButton, BaitsPage, "Наживки");
    }

    private void FishCatalogClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(FishCatalogButton, FishCatalogPage, "Каталог рыбы");
    }

    private void AnalyticsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ShowPage(AnalyticsButton, MainScrollViewer, "Обзор");
        ScrollToSection(AnalyticsButton, AnalyticsSection);
    }

    private void ShowPage(
        System.Windows.Controls.Button button,
        System.Windows.FrameworkElement page,
        string title)
    {
        MainScrollViewer.Visibility = System.Windows.Visibility.Collapsed;
        CatchesPage.Visibility = System.Windows.Visibility.Collapsed;
        CafePage.Visibility = System.Windows.Visibility.Collapsed;
        KeepnetPage.Visibility = System.Windows.Visibility.Collapsed;
        WaterBodiesPage.Visibility = System.Windows.Visibility.Collapsed;
        BaitsPage.Visibility = System.Windows.Visibility.Collapsed;
        FishCatalogPage.Visibility = System.Windows.Visibility.Collapsed;

        page.Visibility = System.Windows.Visibility.Visible;
        PageTitleText.Text = $"/  {title}";
        SetActiveNavigation(button);
    }

    private void SettingsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        SetActiveNavigation((System.Windows.Controls.Button)sender);
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenScreenshotStorageSettingsFromView();
        }
    }

    private void HelpClick(object sender, System.Windows.RoutedEventArgs e)
    {
        SetActiveNavigation((System.Windows.Controls.Button)sender);
        System.Windows.MessageBox.Show(
            "Space — снимок улова.\n" +
            "«Скрин кафе» — снимок ассортимента.\n" +
            "Кнопки «Импорт» и «Экспорт» работают с JSON и .rf4backup.",
            "Помощь",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ToggleSidebarClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        SidebarColumn.Width = new System.Windows.GridLength(
            _sidebarCollapsed ? 76 : 236);
        SidebarNavigationPanel.Margin = _sidebarCollapsed
            ? new System.Windows.Thickness(12, 24, 12, 0)
            : new System.Windows.Thickness(18, 24, 18, 0);

        var detailVisibility = _sidebarCollapsed
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
        SidebarBrandText.Visibility = detailVisibility;
        WorkspaceSectionLabel.Visibility = detailVisibility;
        SystemSectionLabel.Visibility = detailVisibility;
        GameStatusCard.Visibility = detailVisibility;

        var navigationButtons = new[]
        {
            OverviewButton,
            CatchesButton,
            WaterBodiesButton,
            CafeButton,
            KeepnetButton,
            BaitsButton,
            FishCatalogButton,
            AnalyticsButton,
            SettingsButton,
            HelpButton
        };

        foreach (var button in navigationButtons)
        {
            button.HorizontalContentAlignment = _sidebarCollapsed
                ? System.Windows.HorizontalAlignment.Center
                : System.Windows.HorizontalAlignment.Left;
            button.Padding = _sidebarCollapsed
                ? new System.Windows.Thickness(8, 7, 8, 7)
                : new System.Windows.Thickness(10, 7, 10, 7);

            if (button.Content is not System.Windows.Controls.StackPanel panel ||
                panel.Children.Count < 2 ||
                panel.Children[0] is not System.Windows.Controls.Border icon ||
                panel.Children[1] is not System.Windows.Controls.TextBlock label)
            {
                continue;
            }

            label.Visibility = detailVisibility;
            icon.Margin = _sidebarCollapsed
                ? new System.Windows.Thickness(0)
                : new System.Windows.Thickness(0, 0, 8, 0);
            button.ToolTip = _sidebarCollapsed ? label.Text : null;
        }

        SidebarToggleButton.HorizontalAlignment = _sidebarCollapsed
            ? System.Windows.HorizontalAlignment.Center
            : System.Windows.HorizontalAlignment.Left;
        SidebarToggleButton.Content = _sidebarCollapsed ? "\uE76C" : "\uE76B";
        SidebarToggleButton.ToolTip = _sidebarCollapsed
            ? "Развернуть навигацию"
            : "Свернуть навигацию";
    }

    private void OpenWaterBodyClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement
            { DataContext: RF4AssistantPro.WaterBodies.WaterBodyCatalogItem waterBody } ||
            DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        var window = new WaterBodyGalleryWindow(
            waterBody,
            viewModel.CafeSnapshots.Select(item => item.Snapshot))
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void ScrollToSection(
        System.Windows.Controls.Button button,
        System.Windows.FrameworkElement section)
    {
        SetActiveNavigation(button);
        var point = section.TransformToAncestor(MainScrollViewer)
            .Transform(new System.Windows.Point(0, 0));
        MainScrollViewer.ScrollToVerticalOffset(
            MainScrollViewer.VerticalOffset + point.Y - 18);
    }

    private void SetActiveNavigation(System.Windows.Controls.Button button)
    {
        if (_activeNavigationButton is not null &&
            _activeNavigationButton != button)
        {
            _activeNavigationButton.Background =
                System.Windows.Media.Brushes.Transparent;
            _activeNavigationButton.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(152, 163, 179));
        }

        button.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(27, 48, 75));
        button.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(245, 247, 250));
        _activeNavigationButton = button;
    }

    private void CaptureScreenshotClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.CaptureScreenshotFromView();
        }
    }

    private void BindGameClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.BindGameFromView();
        }
    }

    private void CaptureCafeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.CaptureCafeFromView();
        }
    }

    private void ImportCafeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ImportCafeFromView();
        }
    }

    private void CafeOcrAliasesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenCafeOcrAliasesFromView();
        }
    }

    private void ArmKeepnetCaptureClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ArmKeepnetCaptureFromView();
        }
    }

    private void KeepnetOcrAliasesClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenKeepnetOcrAliasesFromView();
        }
    }

    private void ArmBaitRecognitionClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ArmBaitRecognitionFromView();
        }
    }

    private void ArmWaterBodyRecognitionClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ArmWaterBodyRecognitionFromView();
        }
    }

    private void OpenLogsClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenLogsFromView();
        }
    }

    private void OpenWindowsQaClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenWindowsQaFromView();
        }
    }

    private void ReviewSavedScreenshotClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ReviewSavedScreenshotFromView();
        }
    }

    private void ExportDiagnosticsClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ExportDiagnosticsFromView();
        }
    }

    private void ExportCsvClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ExportCsvFromView();
        }
    }

    private void CafeThumbnailClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element &&
            element.DataContext is CafeSnapshotItemViewModel snapshot &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ShowCafeImageFromView(snapshot.FullImagePath);
        }
    }

    private void LatestCafeImageClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is StatisticsViewModel
                { LatestCafeSnapshot: { } snapshot } &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ShowCafeImageFromView(snapshot.FullImagePath);
        }
    }

    private void OpenFishCatalogClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenFishCatalogFromView();
        }
    }

    private void EditKeepnetRecordClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement
            { Tag: RF4AssistantPro.Models.KeepnetRecord record } &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ReviewKeepnetRecordFromView(record);
        }
    }

    private void EditSelectedKeepnetDoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (KeepnetRecordsGrid.SelectedItem is
                RF4AssistantPro.Models.KeepnetRecord record &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.ReviewKeepnetRecordFromView(record);
        }
    }

    private void OpenFishProfileClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StatisticsViewModel
                { SelectedFishCatalogItem: { } fish } &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenFishProfileFromView(fish);
        }
    }

    private void AddFishClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenFishCatalogFromView(openEditor: true);
        }
    }

    private void EditFishClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement
            { Tag: RF4AssistantPro.Fish.FishCatalogItem item } &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenFishCatalogFromView(editItemId: item.Id);
        }
    }

    private void EditSelectedFishDoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FishCatalogPageGrid.SelectedItem is RF4AssistantPro.Fish.FishCatalogItem item &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenFishCatalogFromView(editItemId: item.Id);
        }
    }

    private void SortFishClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not StatisticsViewModel viewModel ||
            FishSortFieldBox.SelectedItem is not System.Windows.Controls.ComboBoxItem selected)
        {
            return;
        }
        var field = selected.Tag?.ToString() ?? "Name";
        viewModel.SortFishCatalog(field, _fishSortAscending);
        FishSortButton.Content = _fishSortAscending ? "Сортировка: А–Я" : "Сортировка: Я–А";
        _fishSortAscending = !_fishSortAscending;
    }

    private void ShowSelectedFishClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.FilterFishCatalog(
                FishFamilyFilterBox.SelectedItem?.ToString(),
                FishHabitatFilterBox.SelectedItem?.ToString());
        }
    }

    private void ShowAllFishClick(object sender, System.Windows.RoutedEventArgs e)
    {
        FishFamilyFilterBox.SelectedIndex = 0;
        FishHabitatFilterBox.SelectedIndex = 0;
        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.FilterFishCatalog(null, null);
        }
    }

    private void OpenBaitCatalogClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenBaitCatalogFromView();
        }
    }

    private void AddBaitClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenBaitCatalogFromView(openEditor: true);
        }
    }

    private void EditBaitClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement
            {
                Tag: RF4AssistantPro.Baits.BaitCatalogItem item
            } &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenBaitCatalogFromView(editItemId: item.Id);
        }
    }

    private void EditSelectedBaitDoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (BaitCatalogPageGrid.SelectedItem is
                RF4AssistantPro.Baits.BaitCatalogItem item &&
            System.Windows.Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.OpenBaitCatalogFromView(editItemId: item.Id);
        }
    }

    private void SortBaitsClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not StatisticsViewModel viewModel ||
            BaitSortFieldBox.SelectedItem is not
                System.Windows.Controls.ComboBoxItem selected)
        {
            return;
        }

        var field = selected.Tag?.ToString() ?? "DisplayName";
        viewModel.SortBaitCatalog(field, _baitSortAscending);
        BaitSortButton.Content = _baitSortAscending
            ? "Сортировка: А–Я"
            : "Сортировка: Я–А";
        _baitSortAscending = !_baitSortAscending;
    }

    private void ShowSelectedBaitsClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.FilterBaitCatalog(
                BaitCategoryFilterBox.SelectedItem?.ToString(),
                BaitSubcategoryFilterBox.SelectedItem?.ToString());
        }
    }

    private void ShowAllBaitsClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        BaitCategoryFilterBox.SelectedIndex = 0;
        BaitSubcategoryFilterBox.SelectedIndex = 0;
        if (DataContext is StatisticsViewModel viewModel)
        {
            viewModel.FilterBaitCatalog(null, null);
        }
    }
}