using System.Collections.ObjectModel;
using System.IO;
using RF4AssistantPro.Cafe;
using RF4AssistantPro.Models;
using RF4AssistantPro.Services;
using RF4AssistantPro.Storage;

namespace RF4AssistantPro.ViewModels;

public sealed partial class StatisticsViewModel
{
    public ObservableCollection<CatchRecord> CafeCatches { get; } = [];

    public ObservableCollection<CafeSnapshotItemViewModel> CafeSnapshots { get; } = [];
    public CafeSnapshotItemViewModel? LatestCafeSnapshot =>
        CafeSnapshots.FirstOrDefault();
    public void SetCafeError(string message)
    {
        ScreenshotStatus = $"Ошибка скриншота кафе: {message}";
    }

    private void RevalidateStoredCafeMarks()
    {
        try
        {
            var snapshots = CafeSnapshots
                .Select(item => item.Snapshot)
                .OrderBy(item => item.CapturedAt)
                .ToList();
            var clearedCatches = 0;
            var clearedKeepnet = 0;

            var updatedCatches = _allCatches
                .Select(record =>
                {
                    if (!record.IsCafeMatch)
                    {
                        return record;
                    }

                    var snapshot = snapshots
                        .LastOrDefault(item =>
                            item.CapturedAt <= record.CaughtAt);
                    var valid = snapshot is not null &&
                        snapshot.Offers.Any(offer =>
                            IsCafeOfferMatch(record, offer));
                    if (valid)
                    {
                        return record;
                    }

                    clearedCatches++;
                    return record with
                    {
                        IsCafeMatch = false,
                        CafeOfferId = null
                    };
                })
                .ToList();

            var updatedKeepnet = KeepnetRecords
                .Select(record =>
                {
                    if (!record.IsCafeMatch)
                    {
                        return record;
                    }

                    var snapshot = snapshots
                        .LastOrDefault(item =>
                            item.CapturedAt <= record.RecordedAt);
                    var valid = snapshot is not null &&
                        snapshot.Offers.Any(offer =>
                            IsCafeOfferMatch(record, offer));
                    if (valid)
                    {
                        return record;
                    }

                    clearedKeepnet++;
                    return record with
                    {
                        IsCafeMatch = false,
                        CafeOfferId = null
                    };
                })
                .ToList();

            if (clearedCatches == 0 && clearedKeepnet == 0)
            {
                return;
            }

            PersistState(
                updatedCatches,
                snapshots,
                updatedKeepnet);
            _allCatches = updatedCatches;
            Replace(
                KeepnetRecords,
                updatedKeepnet
                    .OrderByDescending(item => item.RecordedAt)
                    .ThenBy(item => item.FishName)
                    .ThenByDescending(item => item.WeightKg));
            RefreshLatestCafeMatches();
            RefreshVisibleData("Локальное хранилище + садок");
            AppLog.Info(
                $"Перепроверены отметки кафе: снято с уловов " +
                $"{clearedCatches}, с записей садка {clearedKeepnet}.");
        }
        catch (Exception exception)
        {
            AppLog.Error(
                "Не удалось перепроверить сохранённые отметки кафе.",
                exception);
        }
    }
    public void DeleteCafeSnapshot(CafeSnapshotItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var result = System.Windows.MessageBox.Show(
            $"Удалить снимок кафе от {item.CapturedAtText}?\n" +
            "Изображение и миниатюра также будут удалены.",
            "Удалить снимок кафе",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var updatedSnapshots = CafeSnapshots
            .Where(existing => !ReferenceEquals(existing, item))
            .Select(existing => existing.Snapshot)
            .ToList();

        try
        {
            PersistState(
                _allCatches,
                updatedSnapshots,
                KeepnetRecords.ToList());
            CafeSnapshots.Remove(item);
            OnPropertyChanged(nameof(LatestCafeSnapshot));

            var deleteErrors = new List<string>();
            DeleteCafeImage(item.FullImagePath, deleteErrors);
            DeleteCafeImage(item.Snapshot.ThumbnailPath, deleteErrors);

            AppLog.Info(
                $"Удалён снимок кафе: {item.Snapshot.CapturedAt:O}; " +
                $"изображение={item.FullImagePath}.");

            ScreenshotStatus = deleteErrors.Count == 0
                ? "Снимок кафе и связанные изображения удалены."
                : "Снимок удалён из программы, но не все файлы удалось " +
                  $"удалить: {string.Join("; ", deleteErrors)}";
        }
        catch (Exception exception)
        {
            AppLog.Error("Не удалось удалить снимок кафе.", exception);
            ScreenshotStatus =
                $"Не удалось удалить снимок кафе: {exception.Message}";
        }
    }
    public void AddCafeSnapshot(CafeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        AppLog.Info(
            $"Добавление снимка кафе: предложений={snapshot.Offers.Count}; " +
            $"файл={snapshot.FullImagePath}.");

        var storedSnapshot = new CafeSnapshot
        {
            CapturedAt = snapshot.CapturedAt,
            FullImagePath = snapshot.FullImagePath,
            ThumbnailPath = snapshot.ThumbnailPath,
            Offers = PreserveOfferIds([], snapshot.Offers),
            OfferMatchedCounts = [],
            MatchedCatchCount = 0
        };

        var updatedSnapshots = CafeSnapshots
            .Select(item => item.Snapshot)
            .Prepend(storedSnapshot)
            .OrderByDescending(item => item.CapturedAt)
            .Take(20)
            .ToList();

        try
        {
            PersistState(
                _allCatches,
                updatedSnapshots,
                KeepnetRecords.ToList());
            Replace(
                CafeSnapshots,
                updatedSnapshots.Select(
                    item => new CafeSnapshotItemViewModel(item)));
            RefreshLatestCafeMatches();
            RefreshVisibleData(DataSourceLabel);
            ScreenshotStatus =
                $"Кафе: найдено предложений {storedSnapshot.Offers.Count}, " +
                "можно начинать рыбалку.";
            AppLog.Info(
                $"Снимок кафе сохранён. История: {CafeSnapshots.Count}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Снимок кафе не сохранён.", exception);
            ScreenshotStatus = $"Кафе распознано, но не сохранено: {exception.Message}";
        }
    }

    public void UpdateLatestCafeOffers(IReadOnlyList<CafeOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        var snapshots = CafeSnapshots
            .Select(item => item.Snapshot)
            .OrderByDescending(item => item.CapturedAt)
            .ToList();
        if (snapshots.Count == 0 || offers.Count == 0)
        {
            return;
        }

        var latest = snapshots[0];
        snapshots[0] = new CafeSnapshot
        {
            CapturedAt = latest.CapturedAt,
            FullImagePath = latest.FullImagePath,
            ThumbnailPath = latest.ThumbnailPath,
            Offers = PreserveOfferIds(latest.Offers, offers),
            OfferMatchedCounts =
                latest.OfferMatchedCounts.ToDictionary(
                    item => item.Key,
                    item => item.Value),
            MatchedCatchCount = latest.MatchedCatchCount
        };
        PersistState(
            _allCatches,
            snapshots,
            KeepnetRecords.ToList());
        Replace(
            CafeSnapshots,
            snapshots.Select(
                snapshot => new CafeSnapshotItemViewModel(snapshot)));
        RevalidateStoredCafeMarks();
        RefreshLatestCafeMatches();
        RefreshVisibleData(DataSourceLabel);
        AppLog.Info(
            $"Последний снимок кафе перепроверен. Предложений: " +
            $"{offers.Count}.");
    }
    private void LoadCafeSnapshots()
    {
        try
        {
            foreach (var snapshot in _cafeStore.Load().OrderByDescending(item => item.CapturedAt))
            {
                CafeSnapshots.Add(new CafeSnapshotItemViewModel(snapshot));
            }

            OnPropertyChanged(nameof(LatestCafeSnapshot));
        }
        catch (Exception exception)
        {
            ScreenshotStatus = $"Не удалось загрузить историю кафе: {exception.Message}";
        }
    }
    private void RefreshLatestCafeMatches()
    {
        var snapshots = CafeSnapshots
            .Select(item => item.Snapshot)
            .OrderByDescending(item => item.CapturedAt)
            .ToList();
        if (snapshots.Count == 0)
        {
            return;
        }

        var rebuilt = RebuildLatestCafeAssignments(
            _allCatches,
            snapshots);
        var updatedCatches = rebuilt.Catches;
        var updated = rebuilt.Snapshots;
        var updatedKeepnet = KeepnetRecords
            .Select(record =>
            {
                var relatedCatch = record.RelatedCatchId is { } catchId
                    ? updatedCatches.FirstOrDefault(
                        item => item.Id == catchId)
                    : updatedCatches.FirstOrDefault(
                        item => item.RelatedKeepnetId == record.Id);
                return relatedCatch is null
                    ? record
                    : record with
                    {
                        IsCafeMatch = relatedCatch.IsCafeMatch,
                        CafeOfferId = relatedCatch.CafeOfferId
                    };
            })
            .ToList();

        PersistState(updatedCatches, updated, updatedKeepnet);
        _allCatches = updatedCatches;
        if (!KeepnetRecords.SequenceEqual(updatedKeepnet))
        {
            Replace(
                KeepnetRecords,
                updatedKeepnet
                    .OrderByDescending(item => item.RecordedAt)
                    .ThenBy(item => item.FishName)
                    .ThenByDescending(item => item.WeightKg));
            OnPropertyChanged(nameof(KeepnetCountText));
        }

        Replace(
            CafeSnapshots,
            updated.Select(
                snapshot =>
                    new CafeSnapshotItemViewModel(snapshot)));
        OnPropertyChanged(nameof(LatestCafeSnapshot));
    }

    private static (
        List<CatchRecord> Catches,
        List<CafeSnapshot> Snapshots)
        RebuildLatestCafeAssignments(
            IReadOnlyList<CatchRecord> catches,
            IReadOnlyList<CafeSnapshot> snapshots)
    {
        var latest = snapshots
            .OrderByDescending(item => item.CapturedAt)
            .FirstOrDefault();
        if (latest is null)
        {
            return (catches.ToList(), snapshots.ToList());
        }

        var assignedCounts = new Dictionary<Guid, int>();
        var updatedCatches = new List<CatchRecord>(catches.Count);
        foreach (var record in catches)
        {
            if (record.NeedsReview)
            {
                updatedCatches.Add(record with
                {
                    IsCafeMatch = false,
                    CafeOfferId = null
                });
                continue;
            }

            if (record.CaughtAt < latest.CapturedAt)
            {
                updatedCatches.Add(record);
                continue;
            }

            var offer = latest.Offers
                .OrderByDescending(item =>
                    item.MinimumWeightGrams ?? decimal.MinValue)
                .ThenBy(item => item.Id)
                .FirstOrDefault(item =>
                {
                    if (!IsCafeOfferMatch(record, item))
                    {
                        return false;
                    }

                    if (item.Quantity <= 0)
                    {
                        return true;
                    }

                    assignedCounts.TryGetValue(
                        item.Id,
                        out var assigned);
                    return assigned < item.Quantity;
                });

            if (offer is null)
            {
                updatedCatches.Add(record with
                {
                    IsCafeMatch = false,
                    CafeOfferId = null
                });
                continue;
            }

            assignedCounts.TryGetValue(offer.Id, out var currentCount);
            assignedCounts[offer.Id] = currentCount + 1;
            updatedCatches.Add(record with
            {
                IsCafeMatch = true,
                CafeOfferId = offer.Id
            });
        }

        var updatedSnapshots = snapshots
            .Select(snapshot =>
                ReferenceEquals(snapshot, latest)
                    ? CopySnapshot(
                        snapshot,
                        assignedCounts.Values.Sum(),
                        assignedCounts)
                    : snapshot)
            .ToList();
        return (updatedCatches, updatedSnapshots);
    }
    private static bool WaterBodiesMatch(string catchWaterBody, string cafeWaterBody)
    {
        if (string.IsNullOrWhiteSpace(catchWaterBody) ||
            string.IsNullOrWhiteSpace(cafeWaterBody))
        {
            return true;
        }

        return NormalizeWaterBody(catchWaterBody) ==
               NormalizeWaterBody(cafeWaterBody);
    }

    private static string NormalizeWaterBody(string name)
    {
        var normalized = NormalizeText(name);
        var prefixes = new[] { "р. ", "река ", "оз. ", "озеро " };

        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                return normalized[prefix.Length..];
            }
        }

        return normalized;
    }

    private static bool HasAvailableCafeOffer(
        CatchRecord newCatch,
        CafeSnapshot snapshot,
        IReadOnlyList<CatchRecord> existingCatches)
    {
        foreach (var offer in snapshot.Offers
                     .OrderByDescending(item => item.MinimumWeightGrams ?? 0m))
        {
            if (!IsCafeOfferMatch(newCatch, offer))
            {
                continue;
            }

            // Нулевое количество означает, что OCR не смог прочитать число.
            // В этом случае не блокируем корректное совпадение.
            if (offer.Quantity <= 0)
            {
                return true;
            }

            var alreadyMatched = existingCatches.Count(record =>
                record.IsCafeMatch &&
                record.CaughtAt >= snapshot.CapturedAt &&
                IsCafeOfferMatch(record, offer));

            if (alreadyMatched < offer.Quantity)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCafeOfferMatch(
        CatchRecord record,
        CafeOffer offer)
    {
        return IsCafeOfferMatch(
            record.FishName,
            record.WaterBodyName,
            record.WeightKg,
            offer);
    }

    private static bool IsCafeOfferMatch(
        KeepnetRecord record,
        CafeOffer offer)
    {
        return IsCafeOfferMatch(
            record.FishName,
            record.WaterBodyName,
            record.WeightKg,
            offer);
    }

    private static bool IsCafeOfferMatch(
        string fishName,
        string waterBodyName,
        decimal weightKg,
        CafeOffer offer)
    {
        return CafeFishNameCanonicalizer.NamesMatch(
                   offer.FishName,
                   fishName) &&
               WaterBodiesMatch(
                   waterBodyName,
                   offer.WaterBodyName) &&
               CafeOfferMatcher.IsWeightEligible(
                   weightKg,
                   offer.MinimumWeightGrams);
    }

    private static CafeSnapshot CopySnapshot(
        CafeSnapshot source,
        int matchedCount,
        IReadOnlyDictionary<Guid, int>? offerMatchedCounts = null)
    {
        return new CafeSnapshot
        {
            CapturedAt = source.CapturedAt,
            FullImagePath = source.FullImagePath,
            ThumbnailPath = source.ThumbnailPath,
            Offers = source.Offers.ToList(),
            OfferMatchedCounts = offerMatchedCounts is null
                ? source.OfferMatchedCounts.ToDictionary(
                    item => item.Key,
                    item => item.Value)
                : offerMatchedCounts.ToDictionary(
                    item => item.Key,
                    item => item.Value),
            MatchedCatchCount = matchedCount
        };
    }

    private static List<CafeOffer> PreserveOfferIds(
        IReadOnlyList<CafeOffer> previous,
        IReadOnlyList<CafeOffer> current)
    {
        var unusedPrevious = previous
            .Select((offer, index) => (offer, index))
            .Where(item => item.offer.Id != Guid.Empty)
            .ToList();
        var result = new List<CafeOffer>(current.Count);

        foreach (var currentOffer in current)
        {
            var matchIndex = currentOffer.Id == Guid.Empty
                ? -1
                : unusedPrevious.FindIndex(item =>
                    item.offer.Id == currentOffer.Id);

            if (matchIndex < 0)
            {
                matchIndex = unusedPrevious.FindIndex(item =>
                    CafeOfferIdentityEquals(item.offer, currentOffer));
            }

            if (matchIndex < 0)
            {
                // Совместимость со старыми данными: если OCR изменил
                // отдельное поле, но сохранил порядок строк, используем
                // прежний ID как последний резервный вариант.
                matchIndex = unusedPrevious.FindIndex(item =>
                    item.index == result.Count);
            }

            var id = matchIndex >= 0
                ? unusedPrevious[matchIndex].offer.Id
                : currentOffer.Id == Guid.Empty
                    ? Guid.NewGuid()
                    : currentOffer.Id;
            if (matchIndex >= 0)
            {
                unusedPrevious.RemoveAt(matchIndex);
            }

            result.Add(CafeOfferCloner.WithId(currentOffer, id));
        }

        return result;
    }

    private static bool CafeOfferIdentityEquals(
        CafeOffer left,
        CafeOffer right)
    {
        return NormalizeText(left.FishName) ==
               NormalizeText(right.FishName) &&
               NormalizeWaterBody(left.WaterBodyName) ==
               NormalizeWaterBody(right.WaterBodyName) &&
               left.MinimumWeightGrams == right.MinimumWeightGrams;
    }


    private static void DeleteCafeImage(
        string path,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var applicationRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "RF4AssistantPro")) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            var screenshotsRoot = Path.GetFullPath(
                PortableDataPaths.ScreenshotsDirectory) +
                Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(
                    applicationRoot,
                    StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(
                    screenshotsRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"внешний файл оставлен: {path}");
                return;
            }

            File.Delete(fullPath);
        }
        catch (Exception exception)
        {
            errors.Add(exception.Message);
        }
    }

}
