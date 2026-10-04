using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Capture;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Input;
using RF4AssistantPro.Ocr;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;
using RF4AssistantPro.ViewModels;

namespace RF4AssistantPro.Composition;

internal static class AppCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        var baitCatalogStore = new BaitCatalogStore();
        var fishCatalogStore = new FishCatalogStore();
        var cafeOcrAliasStore = new CafeOcrAliasStore();
        var keepnetOcrAliasStore = new KeepnetOcrAliasStore();
        var catchRecordStore = new CatchRecordStore();
        var backupService = new Rf4BackupService();
        var dataTransferService = new DataTransferService(
            catchRecordStore,
            backupService);
        var captureWorkflowCoordinator = new CaptureWorkflowCoordinator();
        var diagnosticsRetentionCoordinator =
            new DiagnosticsRetentionCoordinator(
                new ScreenshotStorageService(),
                new ScreenshotRetentionSettingsStore());
        var viewModel = new StatisticsViewModel(
            new StatisticsViewModelDependencies(
                new StatisticsService(),
                catchRecordStore,
                new CafeSnapshotStore(),
                dataTransferService,
                new ImportedDataApplyCoordinator(dataTransferService),
                new KeepnetRecordStore(),
                new FishingSessionStore(),
                baitCatalogStore,
                diagnosticsRetentionCoordinator,
                cafeOcrAliasStore,
                keepnetOcrAliasStore));

        return new MainWindow(new MainWindowServices(
            new ScreenCaptureService(),
            new CatchScreenshotRecognizer(),
            new CafeScreenshotRecognizer(),
            new BaitScreenshotRecognizer(),
            new WaterBodyScreenshotRecognizer(),
            new KeepnetScreenshotRecognizer(),
            new CafeThumbnailService(),
            new CafeScreenshotImportService(),
            cafeOcrAliasStore,
            keepnetOcrAliasStore,
            baitCatalogStore,
            fishCatalogStore,
            new WindowsQaChecklistStore(),
            diagnosticsRetentionCoordinator,
            captureWorkflowCoordinator,
            viewModel,
            static () => new GlobalSpaceListener()));
    }
}

internal sealed record MainWindowServices(
    ScreenCaptureService ScreenCaptureService,
    CatchScreenshotRecognizer CatchScreenshotRecognizer,
    CafeScreenshotRecognizer CafeScreenshotRecognizer,
    BaitScreenshotRecognizer BaitScreenshotRecognizer,
    WaterBodyScreenshotRecognizer WaterBodyScreenshotRecognizer,
    KeepnetScreenshotRecognizer KeepnetScreenshotRecognizer,
    CafeThumbnailService CafeThumbnailService,
    CafeScreenshotImportService CafeScreenshotImportService,
    CafeOcrAliasStore CafeOcrAliasStore,
    KeepnetOcrAliasStore KeepnetOcrAliasStore,
    BaitCatalogStore BaitCatalogStore,
    FishCatalogStore FishCatalogStore,
    WindowsQaChecklistStore WindowsQaChecklistStore,
    DiagnosticsRetentionCoordinator DiagnosticsRetentionCoordinator,
    CaptureWorkflowCoordinator CaptureWorkflowCoordinator,
    StatisticsViewModel ViewModel,
    Func<GlobalSpaceListener> GlobalSpaceListenerFactory);