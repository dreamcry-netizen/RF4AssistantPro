namespace RF4AssistantPro.Services;

/// <summary>
/// Coordinates diagnostics and screenshot-retention workflows without
/// depending on WPF or ViewModel state.
/// </summary>
public sealed class DiagnosticsRetentionCoordinator
{
    private readonly ScreenshotStorageService _storageService;
    private readonly ScreenshotRetentionSettingsStore _settingsStore;

    public DiagnosticsRetentionCoordinator(
        ScreenshotStorageService storageService,
        ScreenshotRetentionSettingsStore settingsStore)
    {
        _storageService = storageService ??
            throw new ArgumentNullException(nameof(storageService));
        _settingsStore = settingsStore ??
            throw new ArgumentNullException(nameof(settingsStore));
    }

    public DiagnosticSummary CollectDiagnostics()
    {
        return DiagnosticSummaryService.Collect();
    }

    public ScreenshotRetentionSettings LoadSettings()
    {
        return _settingsStore.Load();
    }

    public void SaveSettings(ScreenshotRetentionSettings settings)
    {
        _settingsStore.Save(settings);
    }

    public ScreenshotStorageSummary GetStorageSummary(
        IEnumerable<string> protectedPaths)
    {
        return _storageService.GetSummary(protectedPaths);
    }

    public ScreenshotCleanupResult Cleanup(
        ScreenshotRetentionSettings settings,
        IEnumerable<string> protectedPaths)
    {
        _settingsStore.Save(settings);
        var result = _storageService.Cleanup(settings, protectedPaths);
        LogCleanup("Очистка снимков", result);
        return result;
    }

    public ScreenshotCleanupResult? RunAutomaticCleanup(
        IEnumerable<string> protectedPaths)
    {
        var settings = _settingsStore.Load();
        if (!settings.AutoCleanupEnabled)
        {
            return null;
        }

        var result = _storageService.Cleanup(settings, protectedPaths);
        if (result.DeletedFileCount > 0 || result.Errors.Count > 0)
        {
            LogCleanup("Автоочистка снимков", result);
        }

        return result;
    }

    public string CreateDiagnosticArchive(bool includeRecentScreenshots)
    {
        var path = DiagnosticExportService.Create(includeRecentScreenshots);
        AppLog.Info($"Создан диагностический архив: {path}.");
        return path;
    }

    private static void LogCleanup(
        string operation,
        ScreenshotCleanupResult result)
    {
        AppLog.Info(
            $"{operation}: удалено={result.DeletedFileCount}; " +
            $"освобождено={ScreenshotStorageSummary.FormatBytes(
                result.FreedBytes)}; ошибок={result.Errors.Count}.");
    }
}