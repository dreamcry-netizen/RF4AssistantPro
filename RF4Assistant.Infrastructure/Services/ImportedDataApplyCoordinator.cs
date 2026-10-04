using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Models;
using RF4AssistantPro.Storage;

namespace RF4AssistantPro.Services;

public sealed record ImportedDataTargetPaths(
    string CatchesPath,
    string CafeSnapshotsPath,
    string KeepnetPath,
    string SessionsPath,
    string BaitCatalogPath,
    string UnrecognizedBaitsPath,
    string CafeOcrAliasesPath,
    string KeepnetOcrAliasesPath);

public sealed record ImportedDataState(
    IReadOnlyList<CatchRecord> Catches,
    IReadOnlyList<CafeSnapshot> CafeSnapshots,
    IReadOnlyList<KeepnetRecord> Keepnet,
    IReadOnlyList<FishingSession> Sessions,
    bool BaitCatalogImported,
    bool CafeOcrAliasesImported,
    bool KeepnetOcrAliasesImported);

/// <summary>
/// Imports, normalizes and atomically persists a complete user-data state.
/// UI collections are refreshed by the caller from the returned snapshot.
/// </summary>
public sealed class ImportedDataApplyCoordinator
{
    private readonly DataTransferService _dataTransferService;

    public ImportedDataApplyCoordinator(
        DataTransferService dataTransferService)
    {
        _dataTransferService = dataTransferService ??
            throw new ArgumentNullException(nameof(dataTransferService));
    }

    public ImportedDataState ImportBackupAndApply(
        string path,
        ImportedDataTargetPaths targets)
    {
        BackupData? staged = null;
        try
        {
            staged = _dataTransferService.ImportBackup(path);
            return ApplyBackup(staged, targets);
        }
        catch
        {
            if (staged is not null)
            {
                try
                {
                    _dataTransferService.CleanupFailedImport(staged);
                }
                catch (Exception cleanupException)
                {
                    AppLog.Error(
                        "Не удалось очистить файлы незавершённого импорта.",
                        cleanupException);
                }
            }

            throw;
        }
    }

    public ImportedDataState ApplyBackup(
        BackupData backup,
        ImportedDataTargetPaths targets)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(targets);

        var catches = backup.Catches.ToList();
        var snapshots = backup.CafeSnapshots
            .OrderByDescending(item => item.CapturedAt)
            .Take(20)
            .ToList();
        var keepnet = backup.Keepnet.ToList();
        var sessions = NormalizeSessions(backup.Sessions);
        var transactionItems = CreateCoreItems(
            targets,
            catches,
            snapshots,
            keepnet,
            sessions);

        if (backup.HasBaitCatalog)
        {
            var baitCatalog = backup.BaitCatalog
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Subcategory)
                .ThenBy(item => item.DisplayName)
                .ToList();
            var unrecognizedBaits = backup.UnrecognizedBaits
                .OrderByDescending(item => item.CapturedAt)
                .Take(100)
                .ToList();
            transactionItems.Add(new JsonFileTransactionItem(
                targets.BaitCatalogPath,
                JsonFileTransaction.Serialize(baitCatalog)));
            transactionItems.Add(new JsonFileTransactionItem(
                targets.UnrecognizedBaitsPath,
                JsonFileTransaction.Serialize(unrecognizedBaits)));
        }

        if (backup.HasCafeOcrAliases)
        {
            transactionItems.Add(new JsonFileTransactionItem(
                targets.CafeOcrAliasesPath,
                JsonFileTransaction.Serialize(
                    NormalizeAliases(backup.CafeOcrAliases))));
        }

        if (backup.HasKeepnetOcrAliases)
        {
            transactionItems.Add(new JsonFileTransactionItem(
                targets.KeepnetOcrAliasesPath,
                JsonFileTransaction.Serialize(
                    NormalizeAliases(backup.KeepnetOcrAliases))));
        }

        new JsonFileTransaction().Commit(transactionItems);
        return new ImportedDataState(
            catches,
            snapshots,
            keepnet,
            sessions,
            backup.HasBaitCatalog,
            backup.HasCafeOcrAliases,
            backup.HasKeepnetOcrAliases);
    }

    public ImportedDataState ImportCatchJsonAndApply(
        string path,
        ImportedDataTargetPaths targets,
        IReadOnlyList<CafeSnapshot> currentSnapshots,
        IReadOnlyList<KeepnetRecord> currentKeepnet,
        IReadOnlyList<FishingSession> currentSessions)
    {
        var catches = _dataTransferService.LoadCatchJson(path).ToList();
        var snapshots = currentSnapshots.Take(20).ToList();
        var keepnet = currentKeepnet.ToList();
        var sessions = NormalizeSessions(currentSessions);
        new JsonFileTransaction().Commit(CreateCoreItems(
            targets,
            catches,
            snapshots,
            keepnet,
            sessions));
        return new ImportedDataState(
            catches,
            snapshots,
            keepnet,
            sessions,
            false,
            false,
            false);
    }

    private static List<JsonFileTransactionItem> CreateCoreItems(
        ImportedDataTargetPaths targets,
        IReadOnlyList<CatchRecord> catches,
        IReadOnlyList<CafeSnapshot> snapshots,
        IReadOnlyList<KeepnetRecord> keepnet,
        IReadOnlyList<FishingSession> sessions)
    {
        return
        [
            new JsonFileTransactionItem(
                targets.CatchesPath,
                JsonFileTransaction.Serialize(catches)),
            new JsonFileTransactionItem(
                targets.CafeSnapshotsPath,
                JsonFileTransaction.Serialize(snapshots)),
            new JsonFileTransactionItem(
                targets.KeepnetPath,
                JsonFileTransaction.Serialize(keepnet)),
            new JsonFileTransactionItem(
                targets.SessionsPath,
                JsonFileTransaction.Serialize(sessions))
        ];
    }

    private static List<FishingSession> NormalizeSessions(
        IEnumerable<FishingSession> sessions)
    {
        return sessions
            .OrderByDescending(item => item.StartedAt)
            .Take(500)
            .ToList();
    }

    private static string NormalizeAlias(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static Dictionary<string, string> NormalizeAliases(
        IReadOnlyDictionary<string, string>? aliases)
    {
        return (aliases ?? new Dictionary<string, string>())
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Key) &&
                !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(
                item => NormalizeAlias(item.Key),
                item => NormalizeAlias(item.Value),
                StringComparer.OrdinalIgnoreCase);
    }
}