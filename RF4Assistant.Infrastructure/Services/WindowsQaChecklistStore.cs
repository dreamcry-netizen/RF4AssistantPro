using System.Text.Json;

namespace RF4AssistantPro.Services;

public enum WindowsQaCheckStatus
{
    Pending,
    Passed,
    Failed
}

public sealed record WindowsQaCheckDefinition(
    string Id,
    string Title);

public sealed record WindowsQaCheckResult(
    string Id,
    string Title,
    WindowsQaCheckStatus Status = WindowsQaCheckStatus.Pending,
    DateTime? UpdatedAt = null,
    string Note = "");

public sealed record WindowsQaChecklistState(
    IReadOnlyList<WindowsQaCheckResult> Items)
{
    public int PassedCount => Items.Count(item =>
        item.Status == WindowsQaCheckStatus.Passed);

    public int FailedCount => Items.Count(item =>
        item.Status == WindowsQaCheckStatus.Failed);

    public int PendingCount => Items.Count(item =>
        item.Status == WindowsQaCheckStatus.Pending);

    public bool ReleaseReady =>
        Items.Count > 0 &&
        Items.All(item => item.Status == WindowsQaCheckStatus.Passed);
}

public sealed class WindowsQaChecklistStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<WindowsQaCheckDefinition> Definitions { get; } =
    [
        new("WPF-01", "Проверить запуск WPF-приложения."),
        new("HOOK-01", "Проверить keyboard hook: Space, V, M, C."),
        new("HOOK-02", "Проверить захват карточки улова."),
        new("OCR-01", "Проверить OCR кафе на 8, 9 и 10 карточках."),
        new("OCR-02", "Проверить конфликтующие веса и ручное исправление."),
        new("HOOK-04", "Проверить серию садка и финальный кадр."),
        new("HOOK-05", "Проверить закрытие приложения во время серии садка."),
        new("BACKUP-01", "Проверить backup/restore с реальными PNG."),
        new("EXPORT-01", "Проверить CSV и диагностический ZIP."),
        new("UI-02", "Проверить DPI 100%, 125% и 150%.")
    ];

    private readonly object _sync = new();

    public WindowsQaChecklistStore(
        string? statePath = null,
        string? logPath = null)
    {
        StatePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RF4AssistantPro",
            "windows-qa-checklist.json");
        LogPath = logPath ?? Path.Combine(
            PortableDataPaths.LogsDirectory,
            "WindowsQa.log");
    }

    public string StatePath { get; }

    public string LogPath { get; }

    public WindowsQaChecklistState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(StatePath))
            {
                return CreateDefaultState();
            }

            try
            {
                var stored = JsonSerializer.Deserialize<
                    List<WindowsQaCheckResult>>(
                    File.ReadAllText(StatePath),
                    JsonOptions) ?? [];
                var byId = stored.ToDictionary(
                    item => item.Id,
                    StringComparer.OrdinalIgnoreCase);
                var items = Definitions
                    .Select(definition => byId.TryGetValue(
                        definition.Id,
                        out var result)
                        ? result with
                        {
                            Id = definition.Id,
                            Title = definition.Title
                        }
                        : new WindowsQaCheckResult(
                            definition.Id,
                            definition.Title))
                    .ToList();
                return new WindowsQaChecklistState(items);
            }
            catch (Exception exception)
            {
                AppLog.Warn(
                    $"Не удалось загрузить Windows QA checklist: " +
                    $"{exception.Message}");
                return CreateDefaultState();
            }
        }
    }

    public WindowsQaChecklistState SetStatus(
        string id,
        WindowsQaCheckStatus status,
        string? note = null,
        DateTime? now = null)
    {
        var definition = Definitions.FirstOrDefault(item =>
            item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            throw new ArgumentException(
                $"Неизвестный Windows QA-тест: {id}.",
                nameof(id));
        }

        lock (_sync)
        {
            var current = Load();
            var updatedAt = now ?? DateTime.Now;
            var cleanNote = NormalizeNote(note);
            var items = current.Items
                .Select(item => item.Id.Equals(
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase)
                    ? new WindowsQaCheckResult(
                        definition.Id,
                        definition.Title,
                        status,
                        updatedAt,
                        cleanNote)
                    : item)
                .ToList();
            var state = new WindowsQaChecklistState(items);
            Save(state);
            AppendLog(
                $"{definition.Id}; status={status}; " +
                $"releaseReady={state.ReleaseReady}; note={cleanNote}");
            return state;
        }
    }

    public WindowsQaChecklistState Reset(DateTime? now = null)
    {
        lock (_sync)
        {
            var state = CreateDefaultState();
            Save(state);
            AppendLog(
                $"RESET; status=Pending; at={(now ?? DateTime.Now):O}");
            return state;
        }
    }

    private static WindowsQaChecklistState CreateDefaultState()
    {
        return new WindowsQaChecklistState(
            Definitions.Select(item => new WindowsQaCheckResult(
                item.Id,
                item.Title)).ToList());
    }

    private void Save(WindowsQaChecklistState state)
    {
        var directory = Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{StatePath}.tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(state.Items, JsonOptions));
        File.Move(temporary, StatePath, true);
    }

    private void AppendLog(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                $"{message}{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось записать Windows QA log: " +
                $"{exception.Message}");
        }
    }

    private static string NormalizeNote(string? note)
    {
        return string.Join(
            " ",
            (note ?? "").Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}