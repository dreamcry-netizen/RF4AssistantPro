using RF4AssistantPro.Ocr;
using RF4AssistantPro.Capture;
using RF4AssistantPro.Cafe;
using System.IO;
using RF4AssistantPro.Input;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;
using RF4AssistantPro.ViewModels;
using System.Diagnostics;
using RF4AssistantPro.Baits;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Composition;

namespace RF4AssistantPro;

public partial class MainWindow : System.Windows.Window
{
    private readonly ScreenCaptureService _screenCaptureService;
    private GlobalSpaceListener? _spaceListener;
    private readonly CatchScreenshotRecognizer _screenshotRecognizer;
    private readonly CafeScreenshotRecognizer _cafeScreenshotRecognizer;
    private readonly BaitScreenshotRecognizer _baitScreenshotRecognizer;
    private readonly WaterBodyScreenshotRecognizer _waterBodyScreenshotRecognizer;
    private readonly KeepnetScreenshotRecognizer _keepnetScreenshotRecognizer;
    private readonly CafeThumbnailService _cafeThumbnailService;
    private readonly CafeScreenshotImportService _cafeImportService;
    private readonly CafeOcrAliasStore _cafeOcrAliasStore;
    private readonly KeepnetOcrAliasStore _keepnetOcrAliasStore;
    private readonly BaitCatalogStore _baitCatalogStore;
    private readonly FishCatalogStore _fishCatalogStore;
    private readonly WindowsQaChecklistStore _windowsQaChecklistStore;
    private readonly DiagnosticsRetentionCoordinator
        _diagnosticsRetentionCoordinator;
    private readonly CaptureWorkflowCoordinator _captureWorkflowCoordinator;
    private CancellationTokenSource? _keepnetSeriesCancellation;
    private readonly object _keepnetSeriesPathGate = new();
    private List<string>? _keepnetSeriesPaths;

    private void TitleBarMouseLeftButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeWindow();
            return;
        }

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void ToggleMaximizeWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        ToggleMaximizeWindow();
    }

    private void CloseWindowClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeWindow()
    {
        WindowState = WindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Maximized;
    }

    internal MainWindow(MainWindowServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _screenCaptureService = services.ScreenCaptureService;
        _screenshotRecognizer = services.CatchScreenshotRecognizer;
        _cafeScreenshotRecognizer = services.CafeScreenshotRecognizer;
        _baitScreenshotRecognizer = services.BaitScreenshotRecognizer;
        _waterBodyScreenshotRecognizer = services.WaterBodyScreenshotRecognizer;
        _keepnetScreenshotRecognizer = services.KeepnetScreenshotRecognizer;
        _cafeThumbnailService = services.CafeThumbnailService;
        _cafeImportService = services.CafeScreenshotImportService;
        _cafeOcrAliasStore = services.CafeOcrAliasStore;
        _keepnetOcrAliasStore = services.KeepnetOcrAliasStore;
        _baitCatalogStore = services.BaitCatalogStore;
        _fishCatalogStore = services.FishCatalogStore;
        _windowsQaChecklistStore = services.WindowsQaChecklistStore;
        _diagnosticsRetentionCoordinator =
            services.DiagnosticsRetentionCoordinator;
        _captureWorkflowCoordinator = services.CaptureWorkflowCoordinator;

        AppLog.Info("Создание главного окна.");
        InitializeComponent();
        Title = $"RF4 Assistant Pro v{AppVersion.Number}";
        var viewModel = services.ViewModel;
        DataContext = viewModel;
        viewModel.SetBaitCatalog(_baitCatalogStore.LoadCatalog());
        viewModel.SetFishCatalog(_fishCatalogStore.LoadCatalog());
        Loaded += MainWindowOnLoaded;

        try
        {
            _spaceListener = services.GlobalSpaceListenerFactory();
            _spaceListener.SpacePressed += SpaceListenerOnSpacePressed;
            _spaceListener.VPressed += SpaceListenerOnVPressed;
            _spaceListener.MPressed += SpaceListenerOnMPressed;
            _spaceListener.CPressed += SpaceListenerOnCPressed;
            AppLog.Info("Глобальный обработчик Space установлен.");
        }
        catch (Exception exception)
        {
            // Ошибка клавиатурного хука больше не прерывает запуск.
            AppLog.Error(
                "Не удалось установить глобальный обработчик Space.",
                exception);
            viewModel.SetScreenshotError(
                $"глобальная клавиша Space недоступна: {exception.Message}. " +
                "Используй кнопку «Сделать снимок улова».");
        }

        AppLog.Info("Главное окно создано.");
    }

    private async void MainWindowOnLoaded(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        Loaded -= MainWindowOnLoaded;
        if (DataContext is not StatisticsViewModel viewModel)
        {
            return;
        }

        var latest = viewModel.LatestCafeSnapshot?.Snapshot;
        if (latest is null || !File.Exists(latest.FullImagePath))
        {
            return;
        }

        try
        {
            AppLog.Info(
                "Повторная проверка последнего снимка кафе после обновления.");
            var offers = await _cafeScreenshotRecognizer.RecognizeAsync(
                latest.FullImagePath);
            if (offers.Count > 0)
            {
                viewModel.UpdateLatestCafeOffers(offers);
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось повторно проверить последний снимок кафе: " +
                $"{exception.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        AppLog.Info("Закрытие главного окна.");
        _keepnetSeriesCancellation?.Cancel();
        _keepnetSeriesCancellation?.Dispose();
        if (_spaceListener is not null)
        {
            _spaceListener.SpacePressed -= SpaceListenerOnSpacePressed;
            _spaceListener.VPressed -= SpaceListenerOnVPressed;
            _spaceListener.MPressed -= SpaceListenerOnMPressed;
            _spaceListener.CPressed -= SpaceListenerOnCPressed;
            _spaceListener.Dispose();
        }
        base.OnClosed(e);
    }

    private void SpaceListenerOnSpacePressed(object? sender, EventArgs e)
    {
        if (!_screenCaptureService.IsBoundGameForeground())
        {
            return;
        }

        var state = _captureWorkflowCoordinator.State;
        AppLog.Info(
            $"Нажата Space. captureInProgress={state.CaptureInProgress}.");
        if (!_captureWorkflowCoordinator.TryBeginCapture())
        {
            return;
        }

        // Снимаем экран прямо внутри низкоуровневого обработчика клавиши,
        // до передачи Space игре. Иначе RF4 успевает закрыть карточку улова,
        // и на позднем кадре остаётся только строка чата.
        try
        {
            var path = _screenCaptureService.CaptureBoundGame();
            AppLog.Info(
                $"Карточка улова снята до закрытия: {path}.");
            Dispatcher.BeginInvoke(
                new Action(() => _ = RecognizeCapturedCatchAsync(path)));
        }
        catch (Exception exception)
        {
            LogCaptureFailure(
                "Ошибка мгновенного снимка карточки улова.",
                exception);
            Dispatcher.BeginInvoke(
                new Action(
                    () =>
                    {
                        var viewModel =
                            (StatisticsViewModel)DataContext;
                        viewModel.SetScreenshotError(exception.Message);
                        _captureWorkflowCoordinator.EndCapture();
                    }));
        }
    }

    private void SpaceListenerOnVPressed(object? sender, EventArgs e)
    {
        var state = _captureWorkflowCoordinator.State;
        AppLog.Info(
            $"Нажата V. baitArmed={state.BaitArmed}; " +
            $"captureInProgress={state.CaptureInProgress}.");
        if (!state.BaitArmed ||
            !_screenCaptureService.IsBoundGameForeground())
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() => _ = CaptureBaitAsync()));
    }

    private void SpaceListenerOnMPressed(object? sender, EventArgs e)
    {
        var state = _captureWorkflowCoordinator.State;
        AppLog.Info(
            $"Нажата M. waterBodyArmed={state.WaterBodyArmed}; " +
            $"captureInProgress={state.CaptureInProgress}.");
        if (!state.WaterBodyArmed ||
            !_screenCaptureService.IsBoundGameForeground())
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() => _ = CaptureWaterBodyAsync()));
    }

    private void SpaceListenerOnCPressed(object? sender, EventArgs e)
    {
        var state = _captureWorkflowCoordinator.State;
        AppLog.Info(
            $"Нажата C. keepnetArmed={state.KeepnetArmed}; " +
            $"keepnetSeries={state.KeepnetSeriesRunning}; " +
            $"captureInProgress={state.CaptureInProgress}.");
        if (!_screenCaptureService.IsBoundGameForeground())
        {
            return;
        }

        if (state.KeepnetSeriesRunning)
        {
            try
            {
                lock (_keepnetSeriesPathGate)
                {
                    if (_keepnetSeriesPaths is not null)
                    {
                        var finalPath =
                            _screenCaptureService.CaptureBoundGame(
                                "rf4_keepnet_final");
                        _keepnetSeriesPaths.Add(finalPath);
                        AppLog.Info(
                            $"Финальный снимок садка перед закрытием: " +
                            $"{finalPath}.");
                    }
                }
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    "Не удалось сохранить финальный снимок садка.",
                    exception);
            }

            _keepnetSeriesCancellation?.Cancel();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (DataContext is StatisticsViewModel viewModel)
                {
                    viewModel.SetKeepnetSeriesStopping();
                }
            }));
            return;
        }

        if (!_captureWorkflowCoordinator.TryStartKeepnetSeries())
        {
            return;
        }
        Dispatcher.BeginInvoke(new Action(() => _ = CaptureKeepnetAsync()));
    }
}
