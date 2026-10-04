using System.IO;
using System.Windows.Media.Imaging;
using RF4AssistantPro.Cafe;

namespace RF4AssistantPro.ViewModels;

public sealed class CafeSnapshotItemViewModel
{
    public CafeSnapshotItemViewModel(CafeSnapshot snapshot)
    {
        Snapshot = snapshot;
        ThumbnailImage = LoadImage(snapshot.ThumbnailPath);
    }

    public CafeSnapshot Snapshot { get; }

    public BitmapImage? ThumbnailImage { get; }

    public string FullImagePath => Snapshot.FullImagePath;

    public string CapturedAtText => Snapshot.CapturedAt.ToString("dd.MM.yyyy HH:mm:ss");

    public int OfferCount => Snapshot.Offers.Count;

    public int MatchedCatchCount => Snapshot.MatchedCatchCount;

    public CafeOrderProgress Progress =>
        CafeOrderProgressCalculator.Calculate(Snapshot);

    public bool IsCompleted => Progress.IsCompleted;

    public string ProgressText
    {
        get
        {
            var progress = Progress;
            var summary = progress.HasKnownQuantity
                ? progress.IsCompleted
                    ? $"Заказ выполнен: {progress.TotalRequested} из {progress.TotalRequested}"
                    : $"Осталось рыб: {progress.Remaining} из {progress.TotalRequested}"
                : "Количество рыб не распознано";

            if (progress.UnknownQuantityOfferCount > 0)
            {
                summary +=
                    $" · без количества: {progress.UnknownQuantityOfferCount}";
            }

            var details = progress.Offers
                .Select(offer => offer.HasKnownQuantity
                    ? offer.IsCompleted
                        ? $"✓ {offer.FishName}: выполнено " +
                          $"{offer.Matched} из {offer.Requested}"
                        : $"{offer.FishName}: осталось " +
                          $"{offer.Remaining} из {offer.Requested}"
                    : $"⚠ {offer.FishName}: количество не распознано");
            return summary + Environment.NewLine +
                   string.Join(Environment.NewLine, details);
        }
    }

    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }
}