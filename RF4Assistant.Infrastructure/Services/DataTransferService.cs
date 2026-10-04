using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Models;
using RF4AssistantPro.Statistics;
using RF4AssistantPro.Storage;

namespace RF4AssistantPro.Services;

/// <summary>
/// Coordinates user data import/export without owning UI dialogs or
/// ViewModel state.
/// </summary>
public sealed class DataTransferService
{
    private readonly CatchRecordStore _catchRecordStore;
    private readonly Rf4BackupService _backupService;

    public DataTransferService(
        CatchRecordStore catchRecordStore,
        Rf4BackupService backupService)
    {
        _catchRecordStore = catchRecordStore ??
            throw new ArgumentNullException(nameof(catchRecordStore));
        _backupService = backupService ??
            throw new ArgumentNullException(nameof(backupService));
    }

    public IReadOnlyList<CatchRecord> LoadCatchJson(string path)
    {
        return _catchRecordStore.Load(path);
    }

    public void SaveCatchJson(
        IEnumerable<CatchRecord> catches,
        string path)
    {
        _catchRecordStore.Save(catches, path);
    }

    public BackupData ImportBackup(string path)
    {
        return _backupService.Import(path);
    }

    public void CleanupFailedImport(BackupData backup)
    {
        _backupService.CleanupFailedImport(backup);
    }

    public void ExportBackup(
        string archivePath,
        IEnumerable<CatchRecord> catches,
        IEnumerable<CafeSnapshot> snapshots,
        IEnumerable<KeepnetRecord> keepnet,
        IEnumerable<FishingSession> sessions,
        StatisticsSnapshot statistics,
        IEnumerable<BaitCatalogItem> baitCatalog,
        IEnumerable<UnrecognizedBait> unrecognizedBaits,
        IReadOnlyDictionary<string, string> cafeOcrAliases,
        IReadOnlyDictionary<string, string> keepnetOcrAliases)
    {
        _backupService.Export(
            archivePath,
            catches,
            snapshots,
            keepnet,
            baitCatalog,
            unrecognizedBaits,
            sessions,
            statistics,
            cafeOcrAliases,
            keepnetOcrAliases);
    }
}