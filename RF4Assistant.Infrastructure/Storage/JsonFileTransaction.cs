using System.IO;
using System.Text.Json;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Storage;

public sealed record JsonFileTransactionItem(
    string TargetPath,
    string Content);

/// <summary>
/// Commits several JSON files as one recoverable transaction.
/// </summary>
public sealed class JsonFileTransaction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string TransactionRoot => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "RF4AssistantPro",
        "Transactions");

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    public static void RecoverPending()
    {
        if (!Directory.Exists(TransactionRoot))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     TransactionRoot))
        {
            try
            {
                RecoverDirectory(directory);
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    $"Не удалось восстановить транзакцию JSON: {directory}.",
                    exception);
                throw new IOException(
                    $"Не удалось восстановить транзакцию {directory}.",
                    exception);
            }
        }
    }

    public void Commit(
        IEnumerable<JsonFileTransactionItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var normalized = items
            .Select(item => new JsonFileTransactionItem(
                Path.GetFullPath(item.TargetPath),
                item.Content))
            .ToList();
        if (normalized.Count == 0)
        {
            return;
        }

        var duplicate = normalized
            .GroupBy(item => item.TargetPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Повторяющийся файл транзакции: {duplicate.Key}.",
                nameof(items));
        }

        Directory.CreateDirectory(TransactionRoot);
        var directory = Path.Combine(
            TransactionRoot,
            $"tx-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var manifest = new TransactionManifest
        {
            State = TransactionState.Prepared,
            Files = normalized
                .Select((item, index) => new TransactionFile
                {
                    TargetPath = item.TargetPath,
                    TemporaryName = $"payload-{index}.json",
                    BackupName = $"backup-{index}.json",
                    HadOriginal = File.Exists(item.TargetPath)
                })
                .ToList()
        };

        try
        {
            for (var index = 0; index < normalized.Count; index++)
            {
                File.WriteAllText(
                    Path.Combine(directory, manifest.Files[index].TemporaryName),
                    normalized[index].Content);
            }

            SaveManifest(directory, manifest);

            foreach (var file in manifest.Files.Where(item =>
                         item.HadOriginal))
            {
                File.Copy(
                    file.TargetPath,
                    Path.Combine(directory, file.BackupName),
                    true);
            }

            manifest.State = TransactionState.BackupReady;
            SaveManifest(directory, manifest);

            foreach (var file in manifest.Files)
            {
                var targetDirectory = Path.GetDirectoryName(file.TargetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Move(
                    Path.Combine(directory, file.TemporaryName),
                    file.TargetPath,
                    true);
            }

            manifest.State = TransactionState.Committed;
            SaveManifest(directory, manifest);
            DeleteDirectory(directory);
        }
        catch
        {
            try
            {
                RecoverDirectory(directory);
            }
            catch (Exception recoveryException)
            {
                AppLog.Error(
                    $"Не удалось откатить транзакцию JSON: {directory}.",
                    recoveryException);
            }

            throw;
        }
    }

    private static void RecoverDirectory(string directory)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            DeleteDirectory(directory);
            return;
        }

        var manifest = JsonSerializer.Deserialize<TransactionManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions) ?? throw new InvalidDataException(
            $"Пустой журнал транзакции: {manifestPath}.");

        if (manifest.State == TransactionState.Committed)
        {
            DeleteDirectory(directory);
            return;
        }

        if (manifest.State == TransactionState.BackupReady)
        {
            foreach (var file in manifest.Files)
            {
                var backup = Path.Combine(directory, file.BackupName);
                if (file.HadOriginal && File.Exists(backup))
                {
                    var targetDirectory = Path.GetDirectoryName(
                        file.TargetPath);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(backup, file.TargetPath, true);
                }
                else if (!file.HadOriginal &&
                         File.Exists(file.TargetPath))
                {
                    File.Delete(file.TargetPath);
                }
            }
        }

        DeleteDirectory(directory);
    }

    private static void SaveManifest(
        string directory,
        TransactionManifest manifest)
    {
        var path = Path.Combine(directory, "manifest.json");
        var temporary = $"{path}.tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temporary, path, true);
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class TransactionManifest
    {
        public string State { get; set; } = TransactionState.Prepared;

        public List<TransactionFile> Files { get; set; } = [];
    }

    private sealed class TransactionFile
    {
        public string TargetPath { get; set; } = "";

        public string TemporaryName { get; set; } = "";

        public string BackupName { get; set; } = "";

        public bool HadOriginal { get; set; }
    }

    private static class TransactionState
    {
        public const string Prepared = "prepared";

        public const string BackupReady = "backup-ready";

        public const string Committed = "committed";
    }
}