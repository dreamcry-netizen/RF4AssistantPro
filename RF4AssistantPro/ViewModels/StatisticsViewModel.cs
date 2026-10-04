using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using RF4AssistantPro.Baits;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Fish;
using RF4AssistantPro.Models;
using RF4AssistantPro.Ocr;
using RF4AssistantPro.Services;
using RF4AssistantPro.Statistics;
using RF4AssistantPro.Storage;
using RF4AssistantPro.WaterBodies;

namespace RF4AssistantPro.ViewModels;

public sealed partial class StatisticsViewModel : INotifyPropertyChanged
{
    private readonly StatisticsService _statisticsService;
    private readonly CatchRecordStore _store;
    private readonly CafeSnapshotStore _cafeStore;
    private readonly DataTransferService _dataTransferService;
    private readonly ImportedDataApplyCoordinator _importedDataApplyCoordinator;
    private readonly KeepnetRecordStore _keepnetStore;
    private readonly FishingSessionStore _sessionStore;
    private readonly BaitCatalogStore _baitCatalogStore;
    private List<CatchRecord> _allCatches = [];
    private CatchStatistics _summary = new();
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private string _dataSourceLabel = "";
    private string _statusText = "";
    private string _screenshotStatus = "Пробел — сделать скриншот экрана";
    private string _gameBindingStatus = "Игра не привязана";
    private string _currentBaitName = "";
    private string _currentBaitImagePath = "";
    private string _currentWaterBodyName = "";
    private string _baitCatalogSortField = "";
    private bool _baitCatalogSortAscending = true;
    private List<BaitCatalogItem> _allBaitCatalog = [];
    private string _baitCategoryFilter = "";
    private string _baitSubcategoryFilter = "";
    private string _selectedBaitCategoryFilter = "Все категории";
    private string _selectedBaitSubcategoryFilter = "Все подкатегории";
    private List<FishCatalogItem> _allFishCatalog = [];
    private string _fishCatalogSortField = "";
    private bool _fishCatalogSortAscending = true;
    private string _fishFamilyFilter = "";
    private string _fishHabitatFilter = "";
    private string _selectedFishFamilyFilter = "Все семейства";
    private string _selectedFishHabitatFilter = "Все места";
    private FishCatalogItem? _selectedFishCatalogItem;
    private DiagnosticSummary _diagnostics = new();
    private readonly DiagnosticsRetentionCoordinator
        _diagnosticsRetentionCoordinator;
    private readonly CafeOcrAliasStore _cafeOcrAliasStore;
    private readonly KeepnetOcrAliasStore _keepnetOcrAliasStore;

    public StatisticsViewModel(StatisticsViewModelDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _statisticsService = dependencies.StatisticsService;
        _store = dependencies.CatchRecordStore;
        _cafeStore = dependencies.CafeSnapshotStore;
        _dataTransferService = dependencies.DataTransferService;
        _importedDataApplyCoordinator =
            dependencies.ImportedDataApplyCoordinator;
        _keepnetStore = dependencies.KeepnetRecordStore;
        _sessionStore = dependencies.FishingSessionStore;
        _baitCatalogStore = dependencies.BaitCatalogStore;
        _diagnosticsRetentionCoordinator =
            dependencies.DiagnosticsRetentionCoordinator;
        _cafeOcrAliasStore = dependencies.CafeOcrAliasStore;
        _keepnetOcrAliasStore = dependencies.KeepnetOcrAliasStore;
        _diagnostics = _diagnosticsRetentionCoordinator.CollectDiagnostics();
        JsonFileTransaction.RecoverPending();

        RefreshCommand = new RelayCommand(ApplyFilter);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        LoadCommand = new RelayCommand(LoadFromDisk);
        SaveCommand = new RelayCommand(SaveToDisk);
        ImportCommand = new RelayCommand(ImportFromJson);
        ExportCommand = new RelayCommand(ExportToJson);
        ClearCatchesCommand = new RelayCommand(ClearCatches);
        ClearKeepnetCommand = new RelayCommand(ClearKeepnet);
        RefreshDiagnosticsCommand = new RelayCommand(RefreshDiagnostics);
        StartFishingSessionCommand = new RelayCommand(StartFishingSession);
        StopFishingSessionCommand = new RelayCommand(StopFishingSession);

        LoadFromDisk();
        LoadCafeSnapshots();
        LoadKeepnet();
        LoadFishingSessions();
        RevalidateStoredCafeMarks();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CatchStatistics Summary
    {
        get => _summary;
        private set
        {
            _summary = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<WaterBodyRating> WaterBodies { get; } = [];

    public ObservableCollection<WaterBodyCatalogItem> WaterBodyCatalog { get; } = [];

    public WaterBodyRating KomarinoeStatistics =>
        WaterBodies.FirstOrDefault(item =>
            item.Name.Contains(
                "Комариное",
                StringComparison.OrdinalIgnoreCase))
        ?? new WaterBodyRating
        {
            Name = "оз. Комариное"
        };

    public ObservableCollection<FishChart> FishDistribution { get; } = [];

    public ObservableCollection<BaitChart> BaitDistribution { get; } = [];

    public ObservableCollection<PeriodAnalyticsRow> PeriodAnalytics { get; } = [];

    public ObservableCollection<SessionAnalyticsRow> SessionAnalytics { get; } = [];

    public ObservableCollection<AnalyticsRow> FishAnalytics { get; } = [];

    public ObservableCollection<AnalyticsRow> BaitAnalytics { get; } = [];

    public string AnalyticsSummaryText { get; private set; } =
        "Сводка появится после добавления уловов.";

    public ObservableCollection<CatchRecord> VisibleCatches { get; } = [];


    public ObservableCollection<FishingSession> FishingSessions { get; } = [];

    public DiagnosticSummary Diagnostics
    {
        get => _diagnostics;
        private set
        {
            _diagnostics = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<BaitCatalogItem> BaitCatalog { get; } = [];

    public ObservableCollection<string> BaitCategories { get; } = [];

    public ObservableCollection<string> BaitSubcategories { get; } = [];

    public string BaitCatalogCountText =>
        BaitCatalog.Count == _allBaitCatalog.Count
            ? $"Добавлено: {_allBaitCatalog.Count}"
            : $"Показано: {BaitCatalog.Count} из {_allBaitCatalog.Count}";

    public string SelectedBaitCategoryFilter
    {
        get => _selectedBaitCategoryFilter;
        set
        {
            _selectedBaitCategoryFilter = value;
            OnPropertyChanged();
        }
    }

    public string SelectedBaitSubcategoryFilter
    {
        get => _selectedBaitSubcategoryFilter;
        set
        {
            _selectedBaitSubcategoryFilter = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FishCatalogItem> FishCatalog { get; } = [];

    public ObservableCollection<string> FishFamilies { get; } = [];

    public ObservableCollection<string> FishHabitats { get; } = [];

    public string FishCatalogCountText =>
        FishCatalog.Count == _allFishCatalog.Count
            ? $"Добавлено: {_allFishCatalog.Count}"
            : $"Показано: {FishCatalog.Count} из {_allFishCatalog.Count}";

    public string SelectedFishFamilyFilter
    {
        get => _selectedFishFamilyFilter;
        set
        {
            _selectedFishFamilyFilter = value;
            OnPropertyChanged();
        }
    }

    public string SelectedFishHabitatFilter
    {
        get => _selectedFishHabitatFilter;
        set
        {
            _selectedFishHabitatFilter = value;
            OnPropertyChanged();
        }
    }

    public FishCatalogItem? SelectedFishCatalogItem
    {
        get => _selectedFishCatalogItem;
        set
        {
            _selectedFishCatalogItem = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CatchRecord> GetCatchesForFish(string fishName)
    {
        return _allCatches
            .Where(record => record.FishName.Equals(
                fishName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }


    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            _fromDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            _toDate = value;
            OnPropertyChanged();
        }
    }

    public string DataSourceLabel
    {
        get => _dataSourceLabel;
        private set
        {
            _dataSourceLabel = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string StoragePath => _store.FilePath;

    public string VersionLabel => AppVersion.Label;

    public string CurrentBaitName
    {
        get => _currentBaitName;
        private set
        {
            _currentBaitName = value;
            OnPropertyChanged();
        }
    }

    public string CurrentWaterBodyName
    {
        get => _currentWaterBodyName;
        private set
        {
            _currentWaterBodyName = value;
            OnPropertyChanged();
        }
    }

    public string CurrentBaitImagePath
    {
        get => _currentBaitImagePath;
        private set
        {
            _currentBaitImagePath = value;
            OnPropertyChanged();
        }
    }

    public string ScreenshotStatus
    {
        get => _screenshotStatus;
        private set
        {
            _screenshotStatus = value;
            OnPropertyChanged();
        }
    }

    public string GameBindingStatus
    {
        get => _gameBindingStatus;
        private set
        {
            _gameBindingStatus = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ImportCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand ClearCatchesCommand { get; }

    public ICommand ClearKeepnetCommand { get; }

    public ICommand RefreshDiagnosticsCommand { get; }

    public ICommand StartFishingSessionCommand { get; }

    public ICommand StopFishingSessionCommand { get; }

    public ScreenshotRetentionSettings LoadScreenshotRetentionSettings()
    {
        return _diagnosticsRetentionCoordinator.LoadSettings();
    }

    public ScreenshotStorageSummary GetScreenshotStorageSummary()
    {
        return _diagnosticsRetentionCoordinator.GetStorageSummary(
            GetProtectedScreenshotPaths());
    }

    public void SaveScreenshotRetentionSettings(
        ScreenshotRetentionSettings settings)
    {
        _diagnosticsRetentionCoordinator.SaveSettings(settings);
        StatusText = "Настройки хранения снимков сохранены.";
    }

    public ScreenshotCleanupResult CleanupScreenshots(
        ScreenshotRetentionSettings settings)
    {
        var result = _diagnosticsRetentionCoordinator.Cleanup(
            settings,
            GetProtectedScreenshotPaths());
        RefreshDiagnostics();
        return result;
    }

    public void RunAutomaticScreenshotCleanup()
    {
        _diagnosticsRetentionCoordinator.RunAutomaticCleanup(
            GetProtectedScreenshotPaths());
    }

    public void RefreshDiagnostics()
    {
        Diagnostics = _diagnosticsRetentionCoordinator.CollectDiagnostics();
        StatusText = "Центр диагностики обновлён.";
    }

    public void SetScreenshotCaptured(string path)
    {
        ScreenshotStatus = $"Скриншот сохранён: {path}";
    }

    public void SetScreenshotError(string message)
    {
        ScreenshotStatus = $"Ошибка скриншота: {message}";
    }

    public void SetGameBound(string title)
    {
        GameBindingStatus = $"Привязано: {title}";
    }

    public void SetGameBindingError(string message)
    {
        GameBindingStatus = $"Игра не привязана: {message}";
    }


    public void SetBaitRecognitionArmed()
    {
        ScreenshotStatus =
            "Определение наживки включено. Открой снасть в игре и один раз нажми V.";
    }

    public void SetRecognizedBait(RecognizedBait bait, string screenshotPath)
    {
        ArgumentNullException.ThrowIfNull(bait);
        CurrentBaitName = bait.Name;
        CurrentBaitImagePath = bait.ImagePath;
        ScreenshotStatus =
            $"Наживка определена: {bait.Name}. Она будет добавляться к новым уловам.";
        AppLog.Info($"Наживка определена: {bait.Name}; снимок={screenshotPath}.");
        AppLog.OcrDetails(
            $"OCR наживки: {bait.RawText.Replace(Environment.NewLine, " | ")}.");
    }

    public void SetBaitRecognitionError(string message)
    {
        ScreenshotStatus = $"Наживка не определена: {message}";
    }

    public void SetBaitCatalog(IEnumerable<BaitCatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _allBaitCatalog = items.ToList();
        Replace(
            BaitCategories,
            new[] { "Все категории" }.Concat(
                _allBaitCatalog
                    .Select(item => item.Category)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)));
        Replace(
            BaitSubcategories,
            new[] { "Все подкатегории" }.Concat(
                _allBaitCatalog
                    .Select(item => item.Subcategory)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)));
        OnPropertyChanged(nameof(SelectedBaitCategoryFilter));
        OnPropertyChanged(nameof(SelectedBaitSubcategoryFilter));
        RefreshBaitCatalogView();
        OnPropertyChanged(nameof(BaitCatalogCountText));
    }

    public void SortBaitCatalog(string field, bool ascending)
    {
        _baitCatalogSortField = field;
        _baitCatalogSortAscending = ascending;
        RefreshBaitCatalogView();
    }

    public void FilterBaitCatalog(string? category, string? subcategory)
    {
        SelectedBaitCategoryFilter =
            category is null or "" ? "Все категории" : category;
        SelectedBaitSubcategoryFilter =
            subcategory is null or "" ? "Все подкатегории" : subcategory;
        _baitCategoryFilter =
            SelectedBaitCategoryFilter == "Все категории"
                ? ""
                : SelectedBaitCategoryFilter;
        _baitSubcategoryFilter =
            SelectedBaitSubcategoryFilter == "Все подкатегории"
                ? ""
                : SelectedBaitSubcategoryFilter;
        RefreshBaitCatalogView();
    }

    private void RefreshBaitCatalogView()
    {
        IEnumerable<BaitCatalogItem> filtered = _allBaitCatalog;
        if (!string.IsNullOrWhiteSpace(_baitCategoryFilter))
        {
            filtered = filtered.Where(item => string.Equals(
                item.Category,
                _baitCategoryFilter,
                StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_baitSubcategoryFilter))
        {
            filtered = filtered.Where(item => string.Equals(
                item.Subcategory,
                _baitSubcategoryFilter,
                StringComparison.CurrentCultureIgnoreCase));
        }

        Replace(BaitCatalog, ApplyBaitCatalogSort(filtered));
        OnPropertyChanged(nameof(BaitCatalogCountText));
    }

    private IEnumerable<BaitCatalogItem> ApplyBaitCatalogSort(
        IEnumerable<BaitCatalogItem> items)
    {
        if (string.IsNullOrWhiteSpace(_baitCatalogSortField))
        {
            return items.ToList();
        }

        Func<BaitCatalogItem, string> selector = _baitCatalogSortField switch
        {
            "Brand" => item => item.Brand,
            "Category" => item => item.Category,
            "Subcategory" => item => item.Subcategory,
            _ => item => item.DisplayName
        };

        var comparer = StringComparer.CurrentCultureIgnoreCase;
        return _baitCatalogSortAscending
            ? items.OrderBy(selector, comparer)
                .ThenBy(item => item.DisplayName, comparer)
                .ToList()
            : items.OrderByDescending(selector, comparer)
                .ThenByDescending(item => item.DisplayName, comparer)
                .ToList();
    }

    public void SetFishCatalog(IEnumerable<FishCatalogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _allFishCatalog = items.ToList();
        Replace(
            FishFamilies,
            new[] { "Все семейства" }.Concat(
                _allFishCatalog.Select(item => item.Family)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)));
        Replace(
            FishHabitats,
            new[] { "Все места" }.Concat(
                _allFishCatalog.Select(item => item.Habitat)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)));
        OnPropertyChanged(nameof(SelectedFishFamilyFilter));
        OnPropertyChanged(nameof(SelectedFishHabitatFilter));
        RefreshFishCatalogView();
        SelectedFishCatalogItem ??= FishCatalog.FirstOrDefault();
        ApplyFishImages();
        RefreshVisibleData(DataSourceLabel);
    }

    public void SortFishCatalog(string field, bool ascending)
    {
        _fishCatalogSortField = field;
        _fishCatalogSortAscending = ascending;
        RefreshFishCatalogView();
    }

    public void FilterFishCatalog(string? family, string? habitat)
    {
        SelectedFishFamilyFilter = family is null or "" ? "Все семейства" : family;
        SelectedFishHabitatFilter = habitat is null or "" ? "Все места" : habitat;
        _fishFamilyFilter = SelectedFishFamilyFilter == "Все семейства"
            ? "" : SelectedFishFamilyFilter;
        _fishHabitatFilter = SelectedFishHabitatFilter == "Все места"
            ? "" : SelectedFishHabitatFilter;
        RefreshFishCatalogView();
    }

    private void RefreshFishCatalogView()
    {
        IEnumerable<FishCatalogItem> filtered = _allFishCatalog;
        if (!string.IsNullOrWhiteSpace(_fishFamilyFilter))
        {
            filtered = filtered.Where(item => string.Equals(
                item.Family, _fishFamilyFilter,
                StringComparison.CurrentCultureIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(_fishHabitatFilter))
        {
            filtered = filtered.Where(item => string.Equals(
                item.Habitat, _fishHabitatFilter,
                StringComparison.CurrentCultureIgnoreCase));
        }

        Func<FishCatalogItem, object> selector = _fishCatalogSortField switch
        {
            "Family" => item => item.Family,
            "Habitat" => item => item.Habitat,
            "TrophyWeightGrams" => item => item.TrophyWeightGrams ?? decimal.MaxValue,
            _ => item => item.Name
        };
        var sorted = _fishCatalogSortAscending
            ? filtered.OrderBy(selector).ThenBy(item => item.Name).ToList()
            : filtered.OrderByDescending(selector).ThenByDescending(item => item.Name).ToList();
        Replace(FishCatalog, sorted);
        if (SelectedFishCatalogItem is not null &&
            FishCatalog.All(item => item.Id != SelectedFishCatalogItem.Id))
        {
            SelectedFishCatalogItem = FishCatalog.FirstOrDefault();
        }
        OnPropertyChanged(nameof(FishCatalogCountText));
    }

    public void SetWaterBodyRecognitionArmed()
    {
        ScreenshotStatus =
            "Определение водоёма включено. Переключись в игру и один раз нажми M.";
    }

    public void SetRecognizedWaterBody(
        RecognizedWaterBody waterBody,
        string screenshotPath)
    {
        ArgumentNullException.ThrowIfNull(waterBody);
        CurrentWaterBodyName = waterBody.Name;
        ScreenshotStatus =
            $"Водоём определён: {waterBody.Name}. Он будет добавляться к новым уловам.";
        AppLog.Info(
            $"Водоём определён: {waterBody.Name}; снимок={screenshotPath}.");
        AppLog.OcrDetails(
            $"OCR водоёма: {waterBody.RawText.Replace(Environment.NewLine, " | ")}.");
    }

    public void SetWaterBodyRecognitionError(string message)
    {
        ScreenshotStatus = $"Водоём не определён: {message}";
    }


    public void AddRecognizedCatch(RecognizedCatch recognized, string screenshotPath)
    {
        ArgumentNullException.ThrowIfNull(recognized);
        AppLog.Info(
            $"Подготовка улова: рыба={recognized.FishName}; " +
            $"вес={recognized.WeightKg}; OCR-водоём={recognized.WaterBodyName}; " +
            $"текущий водоём={CurrentWaterBodyName}; " +
            $"текущая наживка={CurrentBaitName}; снимок={screenshotPath}.");

        var record = new CatchRecord
        {
            Id = Guid.NewGuid(),
            FishingSessionId = ActiveFishingSession?.Id,
            CaughtAt = DateTime.Now,
            Source = RecordSource.Space,
            FishName = recognized.FishName,
            WaterBodyName = string.IsNullOrWhiteSpace(CurrentWaterBodyName)
                ? recognized.WaterBodyName
                : CurrentWaterBodyName,
            BaitName = CurrentBaitName,
            BaitImagePath = CurrentBaitImagePath,
            WeightKg = recognized.WeightKg,
            LengthCm = recognized.LengthCm,
            Quality = recognized.Quality,
            ScreenshotPath = screenshotPath,
            ScreenshotHash =
                RecordIdentityService.TryComputeScreenshotHash(
                    screenshotPath)
        };

        var updatedSnapshots = CafeSnapshots
            .Select(item => item.Snapshot)
            .ToList();
        var updatedCatches = _allCatches.Append(record).ToList();
        var rebuilt = RebuildLatestCafeAssignments(
            updatedCatches,
            updatedSnapshots);
        updatedCatches = rebuilt.Catches;
        updatedSnapshots = rebuilt.Snapshots;
        record = updatedCatches.First(item => item.Id == record.Id);
        var isCafeMatch = record.IsCafeMatch;
        var updatedKeepnet = BuildKeepnetWithCaughtFish(record);

        AppLog.Info(
            $"Результат сопоставления улова: кафе={isCafeMatch}; " +
            $"предложение={record.CafeOfferId?.ToString() ?? "нет"}; " +
            $"водоём={record.WaterBodyName}; наживка={record.BaitName}.");

        try
        {
            PersistState(
                updatedCatches,
                updatedSnapshots,
                updatedKeepnet);
            _allCatches = updatedCatches;
            Replace(
                CafeSnapshots,
                updatedSnapshots
                    .OrderByDescending(item => item.CapturedAt)
                    .Select(snapshot => new CafeSnapshotItemViewModel(snapshot)));
            Replace(
                KeepnetRecords,
                updatedKeepnet
                    .OrderByDescending(item => item.RecordedAt)
                    .ThenBy(item => item.FishName)
                    .ThenByDescending(item => item.WeightKg));
            OnPropertyChanged(nameof(KeepnetCountText));
            RefreshVisibleData("Локальное хранилище");
            ScreenshotStatus =
                $"Улов добавлен в «Уловы» и «Садок»: " +
                $"{recognized.FishName}, " +
                $"{WeightDisplayFormatter.Format(recognized.WeightKg)}" +
                (isCafeMatch ? " — подходит для кафе." : ".");
            AppLog.Info(
                $"Улов сохранён. Всего записей: {_allCatches.Count}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Улов не сохранён.", exception);
            ScreenshotStatus =
                $"Улов распознан, но не сохранён: {exception.Message}";
        }
    }

    public bool IsScreenshotAlreadySaved(string screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(screenshotPath);
        var hash =
            RecordIdentityService.TryComputeScreenshotHash(screenshotPath);
        return _allCatches.Any(record =>
            !string.IsNullOrWhiteSpace(record.ScreenshotPath) &&
            (string.Equals(
                 Path.GetFullPath(record.ScreenshotPath),
                 fullPath,
                 StringComparison.OrdinalIgnoreCase) ||
             hash.Length > 0 &&
             string.Equals(
                 record.ScreenshotHash,
                 hash,
                 StringComparison.OrdinalIgnoreCase)));
    }

    public string ExportCsvArchive()
    {
        var combinedRecords = BuildCombinedRecords();
        var analytics = _statisticsService.GetStatistics(
            combinedRecords,
            FishingSessions);
        var path = CsvExportService.Create(
            combinedRecords,
            KeepnetRecords,
            CafeSnapshots.Select(item => item.Snapshot),
            FishingSessions,
            analytics);
        AppLog.Info(
            $"Создан CSV-экспорт: уловов={_allCatches.Count}; " +
            $"садок={KeepnetRecords.Count}; " +
            $"снимков кафе={CafeSnapshots.Count}; файл={path}.");
        return path;
    }

    public void SetRecognitionError(string message)
    {
        ScreenshotStatus = $"Скриншот сохранён, но улов не распознан: {message}";
    }


    private void LoadFromDisk()
    {
        try
        {
            _allCatches = _store.Load().ToList();

            if (_allCatches.Count == 0)
            {
                RefreshVisibleData("Локальное хранилище");
                StatusText = "Сохранённых уловов пока нет.";
                return;
            }

            RefreshVisibleData("Локальное хранилище");
        }
        catch (Exception exception)
        {
            _allCatches = [];
            RefreshVisibleData("Ошибка хранилища");
            StatusText =
                $"Не удалось загрузить JSON. Данные не заменены демо-записями: {exception.Message}";
        }
    }


    private void SaveToDisk()
    {
        try
        {
            PersistState(
                _allCatches,
                CafeSnapshots.Select(item => item.Snapshot).ToList(),
                KeepnetRecords.ToList());
            DataSourceLabel = "Локальное хранилище";
            StatusText = $"Сохранено записей: {_allCatches.Count}. Файл: {_store.FilePath}";
        }
        catch (Exception exception)
        {
            StatusText = $"Ошибка сохранения: {exception.Message}";
        }
    }

    private void ImportFromJson()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter =
                "Архив RF4 (*.rf4backup)|*.rf4backup|" +
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Импорт данных RF4"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ImportedDataState imported;
            if (Path.GetExtension(dialog.FileName)
                .Equals(".rf4backup", StringComparison.OrdinalIgnoreCase))
            {
                imported =
                    _importedDataApplyCoordinator.ImportBackupAndApply(
                        dialog.FileName,
                        CreateImportedDataTargets());
            }
            else
            {
                imported =
                    _importedDataApplyCoordinator.ImportCatchJsonAndApply(
                        dialog.FileName,
                        CreateImportedDataTargets(),
                        CafeSnapshots.Select(item => item.Snapshot).ToList(),
                        KeepnetRecords.ToList(),
                        FishingSessions.ToList());
            }

            ApplyImportedDataState(imported);
            ClearFilter();
            DataSourceLabel = $"Импорт: {Path.GetFileName(dialog.FileName)}";
            StatusText =
                $"Импортировано уловов: {_allCatches.Count}; " +
                $"снимков кафе: {CafeSnapshots.Count}.";
        }
        catch (Exception exception)
        {
            StatusText = $"Ошибка импорта: {exception.Message}";
        }
    }

    private ImportedDataTargetPaths CreateImportedDataTargets()
    {
        return new ImportedDataTargetPaths(
            _store.FilePath,
            _cafeStore.FilePath,
            _keepnetStore.FilePath,
            _sessionStore.FilePath,
            _baitCatalogStore.CatalogPath,
            _baitCatalogStore.UnrecognizedPath,
            _cafeOcrAliasStore.FilePath,
            _keepnetOcrAliasStore.FilePath);
    }

    private void ApplyImportedDataState(ImportedDataState imported)
    {
        _allCatches = imported.Catches.ToList();
        Replace(
            CafeSnapshots,
            imported.CafeSnapshots.Select(
                snapshot => new CafeSnapshotItemViewModel(snapshot)));
        Replace(
            KeepnetRecords,
            imported.Keepnet
                .OrderByDescending(item => item.RecordedAt)
                .ThenBy(item => item.FishName)
                .ThenByDescending(item => item.WeightKg));
        OnPropertyChanged(nameof(KeepnetCountText));
        Replace(
            FishingSessions,
            imported.Sessions.OrderByDescending(item => item.StartedAt));
        OnPropertyChanged(nameof(ActiveFishingSession));
        OnPropertyChanged(nameof(FishingSessionStatus));
        OnPropertyChanged(nameof(FishingSessionCountText));

        if (imported.BaitCatalogImported)
        {
            SetBaitCatalog(_baitCatalogStore.LoadCatalog());
        }
    }

    private void ExportToJson()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter =
                "Архив RF4 (*.rf4backup)|*.rf4backup|" +
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "rf4-backup.rf4backup",
            DefaultExt = ".rf4backup",
            Title = "Экспорт данных RF4"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (Path.GetExtension(dialog.FileName)
                .Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                _dataTransferService.SaveCatchJson(
                    _allCatches,
                    dialog.FileName);
            }
            else
            {
                var analytics = _statisticsService.GetStatistics(
                    BuildCombinedRecords(),
                    FishingSessions);
                _dataTransferService.ExportBackup(
                    dialog.FileName,
                    _allCatches,
                    CafeSnapshots.Select(item => item.Snapshot),
                    KeepnetRecords,
                    FishingSessions,
                    analytics,
                    _baitCatalogStore.LoadCatalog(),
                    _baitCatalogStore.LoadUnrecognized(),
                    _cafeOcrAliasStore.Load(),
                    _keepnetOcrAliasStore.Load());
            }

            StatusText =
                $"Экспортировано уловов: {_allCatches.Count}; " +
                $"снимков кафе: {CafeSnapshots.Count}.";
        }
        catch (Exception exception)
        {
            StatusText = $"Ошибка экспорта: {exception.Message}";
        }
    }

    private void ApplyFilter()
    {
        RefreshVisibleData(DataSourceLabel);
    }

    private void ClearFilter()
    {
        FromDate = null;
        ToDate = null;
        RefreshVisibleData(DataSourceLabel);
    }

    private void ClearCatches()
    {
        var result = System.Windows.MessageBox.Show(
            "Удалить все записи уловов? Снимки кафе и скриншоты останутся.",
            "Очистить уловы",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            PersistState(
                Array.Empty<CatchRecord>(),
                CafeSnapshots.Select(item => item.Snapshot).ToList(),
                KeepnetRecords.ToList());
            _allCatches.Clear();
            RefreshVisibleData("Локальное хранилище");
            StatusText = "Все уловы очищены.";
        }
        catch (Exception exception)
        {
            StatusText = $"Не удалось очистить уловы: {exception.Message}";
        }
    }


    private void RefreshVisibleData(string sourceLabel)
    {
        ApplyFishImages();
        var combinedRecords = BuildCombinedRecords();
        var filtered = combinedRecords
            .Where(record => !FromDate.HasValue || record.CaughtAt.Date >= FromDate.Value.Date)
            .Where(record => !ToDate.HasValue || record.CaughtAt.Date <= ToDate.Value.Date)
            .OrderByDescending(record => record.CaughtAt)
            .ToList();

        var snapshot = _statisticsService.GetStatistics(
            filtered,
            FishingSessions);
        Summary = snapshot.Summary;

        Replace(WaterBodies, snapshot.WaterBodies);
        RefreshWaterBodyCatalog();
        OnPropertyChanged(nameof(KomarinoeStatistics));
        Replace(FishDistribution, snapshot.FishDistribution);
        Replace(BaitDistribution, snapshot.BaitDistribution);
        Replace(PeriodAnalytics, snapshot.PeriodAnalytics);
        Replace(SessionAnalytics, snapshot.SessionAnalytics);
        var fishAnalytics = snapshot.FishAnalytics.ToList();
        foreach (var row in fishAnalytics)
        {
            row.FishImagePath = ResolveFishImage(row.Label);
        }
        Replace(FishAnalytics, fishAnalytics);
        Replace(BaitAnalytics, snapshot.BaitAnalytics);
        AnalyticsSummaryText =
            $"Периодов: {PeriodAnalytics.Count}; " +
            $"сессий: {SessionAnalytics.Count}; " +
            $"водоёмов: {WaterBodies.Count}; " +
            $"рыб: {FishAnalytics.Count}; " +
            $"наживок: {BaitAnalytics.Count}.";
        OnPropertyChanged(nameof(AnalyticsSummaryText));
        Replace(VisibleCatches, filtered);
        Replace(
            CafeCatches,
            combinedRecords
                .Where(record => record.IsCafeMatch)
                .OrderByDescending(record => record.CaughtAt));
        RefreshKeepnetComparison();
        OnPropertyChanged(nameof(LatestCafeSnapshot));
        OnPropertyChanged(nameof(ActiveFishingSession));
        OnPropertyChanged(nameof(FishingSessionStatus));

        DataSourceLabel = sourceLabel;
        StatusText =
            $"Показано записей: {filtered.Count} из " +
            $"{combinedRecords.Count}.";
    }

    private void RefreshWaterBodyCatalog()
    {
        var items = WaterBodyCatalogRegistry.Definitions.Select(definition =>
        {
            var statistics = WaterBodies.FirstOrDefault(item =>
                WaterBodyCatalogRegistry.NamesMatch(item.Name, definition.Name))
                ?? new WaterBodyRating
                {
                    Rank = definition.Order,
                    Name = definition.Name
                };
            return new WaterBodyCatalogItem(definition, statistics);
        });
        Replace(WaterBodyCatalog, items);
    }

    private static System.Windows.Media.Brush GetComparisonBrush(
        string status)
    {
        return status switch
        {
            "Совпадает" => System.Windows.Media.Brushes.LightGreen,
            "В садке больше" => System.Windows.Media.Brushes.Khaki,
            "В истории больше" => System.Windows.Media.Brushes.LightCoral,
            _ => System.Windows.Media.Brushes.LightGray
        };
    }

    private void ApplyFishImages()
    {
        if (_allFishCatalog.Count == 0)
        {
            return;
        }

        _allCatches = _allCatches
            .Select(record => record with
            {
                FishImagePath = ResolveFishImage(record.FishName)
            })
            .ToList();

        Replace(
            KeepnetRecords,
            KeepnetRecords.Select(record => record with
            {
                FishImagePath = ResolveFishImage(record.FishName)
            }));
    }

    private string ResolveFishImage(string fishName)
    {
        return FishImageResolver.Resolve(_allFishCatalog, fishName);
    }

    private List<CatchRecord> BuildCombinedRecords()
    {
        var combined = _allCatches.ToList();
        var recordedCounts = combined
            .GroupBy(GetCatchInventoryKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        foreach (var group in KeepnetRecords
                     .GroupBy(GetKeepnetKey, StringComparer.Ordinal))
        {
            recordedCounts.TryGetValue(
                group.Key,
                out var alreadyRecorded);
            var missing = Math.Max(
                0,
                group.Count() - alreadyRecorded);
            foreach (var keepnet in group.Take(missing))
            {
                combined.Add(new CatchRecord
                {
                    Id = keepnet.RelatedCatchId ?? keepnet.Id,
                    FishingSessionId = keepnet.FishingSessionId,
                    CaughtAt = keepnet.RecordedAt,
                    Source = keepnet.Source,
                    FishName = keepnet.FishName,
                    WaterBodyName =
                        string.IsNullOrWhiteSpace(keepnet.WaterBodyName)
                            ? "—"
                            : keepnet.WaterBodyName,
                    BaitName = "—",
                    WeightKg = keepnet.WeightKg,
                    Quality = keepnet.NeedsReview
                        ? "Требует проверки"
                        : "",
                    ScreenshotPath = keepnet.ScreenshotPath,
                    ScreenshotHash = keepnet.ScreenshotHash,
                    CafeOfferId = keepnet.CafeOfferId,
                    RelatedKeepnetId = keepnet.Id,
                    FishImagePath = keepnet.FishImagePath,
                    IsCafeMatch = keepnet.IsCafeMatch,
                    NeedsReview = keepnet.NeedsReview
                });
            }
        }

        return combined;
    }

    private static string GetCatchInventoryKey(CatchRecord record)
    {
        return $"{NormalizeText(record.FishName)}|" +
               record.WeightKg.ToString(
                   "0.######",
                   System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        ObservableCollectionReplaceHelper.Replace(target, source);
    }

    private static string DisplayOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string NormalizeText(string name)
    {
        return string.Join(
            " ",
            name.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }


    private void PersistState(
        IReadOnlyList<CatchRecord> catches,
        IReadOnlyList<CafeSnapshot> snapshots,
        IReadOnlyList<KeepnetRecord>? keepnet = null,
        IReadOnlyList<FishingSession>? sessions = null,
        IReadOnlyList<BaitCatalogItem>? baitCatalog = null,
        IReadOnlyList<UnrecognizedBait>? unrecognizedBaits = null)
    {
        var targetKeepnet = keepnet ?? KeepnetRecords.ToList();
        var targetSessions = sessions ?? FishingSessions.ToList();
        var normalizedCatches = catches.ToList();
        var normalizedSnapshots = snapshots.Take(20).ToList();
        var normalizedKeepnet = targetKeepnet.ToList();
        var normalizedSessions = targetSessions
            .OrderByDescending(item => item.StartedAt)
            .Take(500)
            .ToList();

        var transactionItems = new List<JsonFileTransactionItem>
        {
            new JsonFileTransactionItem(
                _store.FilePath,
                JsonFileTransaction.Serialize(normalizedCatches)),
            new JsonFileTransactionItem(
                _cafeStore.FilePath,
                JsonFileTransaction.Serialize(normalizedSnapshots)),
            new JsonFileTransactionItem(
                _keepnetStore.FilePath,
                JsonFileTransaction.Serialize(normalizedKeepnet)),
            new JsonFileTransactionItem(
                _sessionStore.FilePath,
                JsonFileTransaction.Serialize(normalizedSessions))
        };

        if (baitCatalog is not null &&
            unrecognizedBaits is not null)
        {
            var normalizedBaitCatalog = baitCatalog
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Subcategory)
                .ThenBy(item => item.DisplayName)
                .ToList();
            var normalizedUnrecognizedBaits = unrecognizedBaits
                .OrderByDescending(item => item.CapturedAt)
                .Take(100)
                .ToList();
            transactionItems.Add(
                new JsonFileTransactionItem(
                    _baitCatalogStore.CatalogPath,
                    JsonFileTransaction.Serialize(
                        normalizedBaitCatalog)));
            transactionItems.Add(
                new JsonFileTransactionItem(
                    _baitCatalogStore.UnrecognizedPath,
                    JsonFileTransaction.Serialize(
                        normalizedUnrecognizedBaits)));
        }

        new JsonFileTransaction().Commit(transactionItems);
    }


    private IEnumerable<string> GetProtectedScreenshotPaths()
    {
        foreach (var record in _allCatches)
        {
            yield return record.ScreenshotPath;
        }

        foreach (var record in KeepnetRecords)
        {
            yield return record.ScreenshotPath;
        }

        foreach (var snapshot in CafeSnapshots.Select(item => item.Snapshot))
        {
            yield return snapshot.FullImagePath;
            yield return snapshot.ThumbnailPath;
        }

        foreach (var item in _baitCatalogStore.LoadUnrecognized())
        {
            yield return item.ScreenshotPath;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}