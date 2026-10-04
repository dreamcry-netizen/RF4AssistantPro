using System.Collections.ObjectModel;
using RF4AssistantPro.Models;
using RF4AssistantPro.Ocr;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;

namespace RF4AssistantPro.ViewModels;

public sealed partial class StatisticsViewModel
{
    public ObservableCollection<KeepnetRecord> KeepnetRecords { get; } = [];

    public ObservableCollection<KeepnetComparisonRowViewModel>
        KeepnetComparison { get; } = [];

    public string KeepnetCountText => $"Рыб в записи: {KeepnetRecords.Count}";

    public string KeepnetComparisonText { get; private set; } =
        "Сверка с историей ещё не выполнена.";

    public string KeepnetReviewText { get; private set; } =
        "Все записи распознаны.";

    public void SetKeepnetCaptureArmed()
    {
        ScreenshotStatus =
            "Запись садка включена. Откройте садок одним нажатием C. " +
            "После сигнала прокручивайте список вниз каждые 3 секунды.";
    }

    public void SetKeepnetSeriesCapturing(int current)
    {
        ScreenshotStatus =
            $"Серия садка: сделан снимок {current}. " +
            "Прокрутите список; для завершения снова нажмите C.";
    }

    public void SetKeepnetSeriesStopping()
    {
        ScreenshotStatus =
            "Серия остановлена. Подготавливаю распознавание снимков…";
    }

    public void SetKeepnetSeriesProcessing(int screenshotCount)
    {
        ScreenshotStatus =
            $"Серия из {screenshotCount} снимков готова. " +
            "Распознаю карточки садка…";
    }

    public void SetKeepnetSeriesComplete(
        int screenshotCount,
        int added,
        int skipped,
        int needsReview)
    {
        ScreenshotStatus =
            $"Садок записан: снимков {screenshotCount}, добавлено {added}, " +
            $"повторов пропущено {skipped}, требуют проверки " +
            $"{needsReview}.";
    }

    public void SetKeepnetError(string message)
    {
        ScreenshotStatus = $"Садок не записан: {message}";
    }

    public (int Added, int Skipped) AddKeepnetSnapshot(
        IEnumerable<RecognizedKeepnetFish> recognized,
        string screenshotPath,
        int? expectedTotalCount = null)
    {
        ArgumentNullException.ThrowIfNull(recognized);
        var recognizedItems = recognized.ToList();
        var existingCatchCountBefore = _allCatches.Count;
        var existing = KeepnetRecords.ToList();
        var screenshotHash =
            RecordIdentityService.TryComputeScreenshotHash(screenshotPath);

        // Повторная обработка того же PNG не должна создавать новые
        // экземпляры. Сравнение только по рыбе и весу намеренно оставляем
        // ниже: две реальные одинаковые рыбы должны сохраняться отдельно.
        if (screenshotHash.Length > 0 &&
            existing.Any(record =>
                record.Source == RecordSource.Keepnet &&
                record.ScreenshotHash == screenshotHash))
        {
            AppLog.Info(
                $"Снимок садка уже обработан, повтор пропущен: " +
                $"{screenshotPath}.");
            return (0, recognizedItems.Count);
        }

        var existingCounts = existing
            .GroupBy(GetKeepnetKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        var added = 0;
        var skipped = 0;
        var newlyAdded = new List<KeepnetRecord>();
        var now = DateTime.Now;
        var latestCafe = CafeSnapshots
            .OrderByDescending(item => item.Snapshot.CapturedAt)
            .FirstOrDefault()
            ?.Snapshot;

        var candidates = new List<KeepnetRecord>();
        foreach (var fish in recognizedItems)
        {
            var resolvedFishName =
                _keepnetOcrAliasStore.Resolve(fish.FishName);
            var cleanFishName =
                KeepnetNameNormalizer.Clean(resolvedFishName);
            if (!resolvedFishName.Equals(
                    fish.FishName,
                    StringComparison.OrdinalIgnoreCase))
            {
                AppLog.Info(
                    $"Садок: применён обученный OCR-псевдоним " +
                    $"«{fish.FishName}» → «{resolvedFishName}».");
            }

            if (cleanFishName.Length < 3)
            {
                skipped++;
                continue;
            }

            var candidate = new KeepnetRecord
            {
                RecordedAt = now,
                FishingSessionId = ActiveFishingSession?.Id,
                Source = RecordSource.Keepnet,
                FishName = cleanFishName,
                WaterBodyName = CurrentWaterBodyName,
                WeightKg = fish.WeightKg,
                ScreenshotPath = screenshotPath,
                ScreenshotHash = screenshotHash,
                RawOcrText = fish.RawText,
                IsCafeMatch = latestCafe is not null &&
                    latestCafe.Offers.Any(offer =>
                        IsCafeOfferMatch(
                            cleanFishName,
                            CurrentWaterBodyName,
                            fish.WeightKg,
                            offer))
            };

            candidates.Add(candidate);
        }

        var candidateGroups = candidates
            .GroupBy(GetKeepnetKey, StringComparer.Ordinal)
            .ToList();
        var plannedCounts = KeepnetMergePlanner.PlanCountsToAdd(
            existing.Count(record => !record.NeedsReview),
            expectedTotalCount,
            candidateGroups.Select(group =>
            {
                existingCounts.TryGetValue(
                    group.Key,
                    out var alreadyStored);
                return (alreadyStored, group.Count());
            }));

        for (var groupIndex = 0;
             groupIndex < candidateGroups.Count;
             groupIndex++)
        {
            var group = candidateGroups[groupIndex];
            existingCounts.TryGetValue(
                group.Key,
                out var alreadyStored);
            var snapshotItems = group.ToList();
            var toAdd = plannedCounts[groupIndex];
            skipped += snapshotItems.Count - toAdd;
            foreach (var candidate in snapshotItems.Take(toAdd))
            {
                existing.Add(candidate);
                newlyAdded.Add(candidate);
                added++;
            }

            existingCounts[group.Key] = alreadyStored + toAdd;
        }

        var updatedCatches = _allCatches.ToList();
        foreach (var group in newlyAdded
                     .GroupBy(GetKeepnetKey, StringComparer.Ordinal))
        {
            var key = group.Key;
            var currentKeepnetCount = existing.Count(record =>
                GetKeepnetKey(record) == key);
            var existingCatchCount = updatedCatches.Count(record =>
                GetCatchInventoryKey(record) == key);
            var toMirror = KeepnetMergePlanner.GetCountToMirrorToCatches(
                existingCatchCount,
                currentKeepnetCount,
                group.Count());

            foreach (var record in group.Take(toMirror))
            {
                updatedCatches.Add(new CatchRecord
                {
                    Id = Guid.NewGuid(),
                    FishingSessionId = record.FishingSessionId,
                    CaughtAt = record.RecordedAt,
                    Source = RecordSource.MirroredFromKeepnet,
                    FishName = record.FishName,
                    WaterBodyName = record.WaterBodyName,
                    WeightKg = record.WeightKg,
                    LengthCm = null,
                    ScreenshotPath = record.ScreenshotPath,
                    ScreenshotHash = record.ScreenshotHash,
                    RelatedKeepnetId = record.Id,
                    IsCafeMatch = record.IsCafeMatch
                });
            }
        }

        PersistState(
            updatedCatches,
            CafeSnapshots.Select(item => item.Snapshot).ToList(),
            existing);
        _allCatches = updatedCatches;
        Replace(
            KeepnetRecords,
            existing
                .OrderByDescending(item => item.RecordedAt)
                .ThenBy(item => item.FishName)
                .ThenByDescending(item => item.WeightKg));
        OnPropertyChanged(nameof(KeepnetCountText));
        RefreshLatestCafeMatches();
        RefreshVisibleData("Локальное хранилище + садок");
        ScreenshotStatus =
            $"Садок записан: добавлено {added}, пропущено повторов {skipped}.";
        AppLog.Info(
            $"Садок сохранён: добавлено={added}; повторы={skipped}; " +
            $"всего={KeepnetRecords.Count}; добавлено в уловы=" +
            $"{updatedCatches.Count - existingCatchCountBefore}; " +
            $"снимок={screenshotPath}.");
        return (added, skipped);
    }

    public int EnsureKeepnetCount(
        int expectedCount,
        string screenshotPath)
    {
        var realRecords = KeepnetRecords
            .Where(record => !record.NeedsReview)
            .ToList();
        var missing = Math.Max(
            0,
            expectedCount - realRecords.Count);
        var updated = realRecords.ToList();
        for (var index = 0; index < missing; index++)
        {
            updated.Add(new KeepnetRecord
            {
                Id = Guid.NewGuid(),
                RecordedAt = DateTime.Now,
                Source = RecordSource.Keepnet,
                FishName = "Не распознана",
                WaterBodyName = CurrentWaterBodyName,
                WeightKg = 0m,
                NeedsReview = true,
                ScreenshotPath = screenshotPath,
                ScreenshotHash =
                    RecordIdentityService.TryComputeScreenshotHash(
                        screenshotPath)
            });
        }

        PersistState(
            _allCatches,
            CafeSnapshots.Select(item => item.Snapshot).ToList(),
            updated);
        Replace(
            KeepnetRecords,
            updated
                .OrderByDescending(item => item.RecordedAt)
                .ThenBy(item => item.FishName)
                .ThenByDescending(item => item.WeightKg));
        OnPropertyChanged(nameof(KeepnetCountText));
        RefreshLatestCafeMatches();
        RefreshVisibleData("Локальное хранилище + садок");
        AppLog.Info(
            $"Контроль количества садка: в игре={expectedCount}; " +
            $"распознано={realRecords.Count}; требуют проверки={missing}.");
        return missing;
    }

    public bool CorrectKeepnetRecord(
        Guid recordId,
        string fishName,
        decimal weightKg)
    {
        var source = KeepnetRecords.FirstOrDefault(item => item.Id == recordId);
        if (source is null)
        {
            return false;
        }

        var corrected = KeepnetReviewPlanner.ApplyCorrection(
            source,
            fishName,
            weightKg);
        var updatedCatches = _allCatches.ToList();
        var relatedIndex = source.RelatedCatchId.HasValue
            ? updatedCatches.FindIndex(item =>
                item.Id == source.RelatedCatchId.Value)
            : -1;
        Guid relatedCatchId;
        if (relatedIndex >= 0)
        {
            var related = updatedCatches[relatedIndex];
            relatedCatchId = related.Id;
            updatedCatches[relatedIndex] = related with
            {
                FishName = corrected.FishName,
                WeightKg = corrected.WeightKg,
                NeedsReview = false
            };
        }
        else
        {
            relatedCatchId = Guid.NewGuid();
            updatedCatches.Add(new CatchRecord
            {
                Id = relatedCatchId,
                FishingSessionId = corrected.FishingSessionId,
                CaughtAt = corrected.RecordedAt,
                Source = RecordSource.MirroredFromKeepnet,
                FishName = corrected.FishName,
                WaterBodyName = corrected.WaterBodyName,
                WeightKg = corrected.WeightKg,
                ScreenshotPath = corrected.ScreenshotPath,
                ScreenshotHash = corrected.ScreenshotHash,
                RelatedKeepnetId = corrected.Id,
                IsCafeMatch = corrected.IsCafeMatch
            });
        }

        corrected = corrected with { RelatedCatchId = relatedCatchId };
        var updatedKeepnet = KeepnetRecords
            .Select(item => item.Id == recordId ? corrected : item)
            .ToList();

        PersistState(
            updatedCatches,
            CafeSnapshots.Select(item => item.Snapshot).ToList(),
            updatedKeepnet);
        try
        {
            if (_keepnetOcrAliasStore.Learn(
                    source.FishName,
                    corrected.FishName))
            {
                AppLog.Info(
                    $"Садок: обучен OCR-псевдоним " +
                    $"«{source.FishName}» → «{corrected.FishName}».");
            }
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                "Не удалось сохранить OCR-псевдоним садка: " +
                exception.Message);
        }

        _allCatches = updatedCatches;
        Replace(
            KeepnetRecords,
            updatedKeepnet
                .OrderByDescending(item => item.RecordedAt)
                .ThenBy(item => item.FishName)
                .ThenByDescending(item => item.WeightKg));
        ApplyFishImages();
        OnPropertyChanged(nameof(KeepnetCountText));
        RefreshLatestCafeMatches();
        RefreshVisibleData("Локальное хранилище + садок");
        ScreenshotStatus =
            $"Запись садка исправлена: {corrected.FishName}, " +
            $"{WeightDisplayFormatter.Format(corrected.WeightKg)}.";
        AppLog.Info(
            $"Ручная корректировка садка: id={corrected.Id}; " +
            $"рыба={corrected.FishName}; вес={corrected.WeightKg}.");
        return true;
    }

    private void LoadKeepnet()
    {
        try
        {
            var loaded = _keepnetStore.Load();
            var cleaned = CollapseKeepnetOcrAliases(loaded
                .Select(item => item with
                {
                    FishName =
                        KeepnetNameNormalizer.Clean(item.FishName)
                })
                .Where(item => item.FishName.Length >= 3)
                .ToList());
            if (cleaned.Count != loaded.Count ||
                cleaned.Zip(loaded).Any(pair =>
                    pair.First.FishName != pair.Second.FishName))
            {
                new JsonFileTransaction().Commit(
                [
                    new JsonFileTransactionItem(
                        _keepnetStore.FilePath,
                        JsonFileTransaction.Serialize(cleaned))
                ]);
                AppLog.Info(
                    "Исправлены старые названия в записи садка.");
            }

            Replace(
                KeepnetRecords,
                cleaned
                    .OrderByDescending(item => item.RecordedAt)
                    .ThenBy(item => item.FishName)
                    .ThenByDescending(item => item.WeightKg));
            OnPropertyChanged(nameof(KeepnetCountText));
            RefreshVisibleData("Локальное хранилище + садок");
        }
        catch (Exception exception)
        {
            AppLog.Error("Не удалось загрузить садок.", exception);
            ScreenshotStatus =
                $"Не удалось загрузить садок: {exception.Message}";
        }
    }

    private static string GetKeepnetKey(KeepnetRecord record)
    {
        return $"{NormalizeText(record.FishName)}|" +
               record.WeightKg.ToString(
                   "0.######",
                   System.Globalization.CultureInfo.InvariantCulture);
    }

    private static List<KeepnetRecord> CollapseKeepnetOcrAliases(
        IReadOnlyCollection<KeepnetRecord> records)
    {
        return records
            .Where(record =>
            {
                var current = string.Concat(
                    NormalizeText(record.FishName)
                        .Where(char.IsLetterOrDigit));
                return !records.Any(other =>
                    other.WeightKg == record.WeightKg &&
                    other.FishName.Length > record.FishName.Length &&
                    string.Concat(
                            NormalizeText(other.FishName)
                                .Where(char.IsLetterOrDigit))
                        .StartsWith(
                            current,
                            StringComparison.Ordinal));
            })
            .ToList();
    }

    private List<KeepnetRecord> BuildKeepnetWithCaughtFish(
        CatchRecord record)
    {
        var updated = KeepnetRecords.ToList();
        updated.Add(new KeepnetRecord
        {
            Id = Guid.NewGuid(),
            FishingSessionId = record.FishingSessionId,
            RecordedAt = record.CaughtAt,
            Source = RecordSource.MirroredFromCatch,
            FishName = KeepnetNameNormalizer.Clean(record.FishName),
            WaterBodyName = record.WaterBodyName,
            WeightKg = record.WeightKg,
            IsCafeMatch = record.IsCafeMatch,
            ScreenshotPath = record.ScreenshotPath,
            ScreenshotHash = record.ScreenshotHash,
            CafeOfferId = record.CafeOfferId,
            RelatedCatchId = record.Id
        });
        return updated;
    }

    private void ClearKeepnet()
    {
        var result = System.Windows.MessageBox.Show(
            "Удалить все распознанные записи садка?",
            "Очистить садок",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            PersistState(
                _allCatches,
                CafeSnapshots.Select(item => item.Snapshot).ToList(),
                []);
            KeepnetRecords.Clear();
            OnPropertyChanged(nameof(KeepnetCountText));
            RefreshLatestCafeMatches();
            RefreshVisibleData("Локальное хранилище");
            ScreenshotStatus = "Садок очищен.";
            AppLog.Info("Все записи садка очищены пользователем.");
        }
        catch (Exception exception)
        {
            ScreenshotStatus =
                $"Не удалось очистить садок: {exception.Message}";
            AppLog.Error("Не удалось очистить садок.", exception);
        }
    }

    private void RefreshKeepnetComparison()
    {
        var sources = _allCatches
            .Where(record => !record.NeedsReview)
            .Select(record => new
            {
                Key = GetCatchInventoryKey(record),
                record.FishName,
                record.WeightKg,
                IsHistory = true
            })
            .Concat(
                KeepnetRecords
                    .Where(record => !record.NeedsReview)
                    .Select(record => new
                    {
                        Key = GetKeepnetKey(record),
                        record.FishName,
                        record.WeightKg,
                        IsHistory = false
                    }))
            .ToList();

        var rows = sources
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var historyCount = group.Count(item => item.IsHistory);
                var keepnetCount = group.Count(item => !item.IsHistory);
                var status = historyCount == keepnetCount
                    ? "Совпадает"
                    : keepnetCount > historyCount
                        ? "В садке больше"
                        : "В истории больше";

                return new KeepnetComparisonRowViewModel
                {
                    FishName = first.FishName,
                    FishImagePath = ResolveFishImage(first.FishName),
                    WeightKg = first.WeightKg,
                    HistoryCount = historyCount,
                    KeepnetCount = keepnetCount,
                    Status = status,
                    StatusBrush = GetComparisonBrush(status)
                };
            })
            .OrderBy(item => item.Status == "Совпадает")
            .ThenBy(item => item.FishName)
            .ThenBy(item => item.WeightKg)
            .ToList();

        Replace(KeepnetComparison, rows);
        var mismatches = rows.Count(item => item.Status != "Совпадает");
        var matched = rows.Count - mismatches;
        KeepnetComparisonText =
            $"Сверка с историей: совпадает {matched}, " +
            $"расхождений {mismatches}.";
        KeepnetReviewText = KeepnetRecords.Any(record => record.NeedsReview)
            ? $"Требуют ручной проверки: " +
              $"{KeepnetRecords.Count(record => record.NeedsReview)}."
            : "Все записи садка распознаны.";
        OnPropertyChanged(nameof(KeepnetComparisonText));
        OnPropertyChanged(nameof(KeepnetReviewText));
    }
}
