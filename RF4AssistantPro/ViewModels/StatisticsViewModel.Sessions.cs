using RF4AssistantPro.Models;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.ViewModels;

public sealed partial class StatisticsViewModel
{
    public FishingSession? ActiveFishingSession =>
        FishingSessions.FirstOrDefault(item => item.IsActive);

    public string FishingSessionStatus
    {
        get
        {
            var session = ActiveFishingSession;
            if (session is null)
            {
                return "Сессия не запущена. Новые записи сохраняются без сессии.";
            }

            var catchCount = _allCatches.Count(item =>
                item.FishingSessionId == session.Id);
            return $"Активна с {session.StartedAt:dd.MM.yyyy HH:mm}; " +
                   $"уловов: {catchCount}; " +
                   $"водоём: {DisplayOrDash(session.WaterBodyName)}; " +
                   $"наживка: {DisplayOrDash(session.BaitName)}";
        }
    }

    public string FishingSessionCountText =>
        $"Сессий: {FishingSessions.Count}";

    private void StartFishingSession()
    {
        if (ActiveFishingSession is not null)
        {
            StatusText = "Сессия уже запущена.";
            return;
        }

        var updated = FishingSessionPlanner.Start(
            FishingSessions,
            DateTime.Now,
            CurrentWaterBodyName,
            CurrentBaitName).ToList();
        var session = updated[^1];
        try
        {
            PersistState(
                _allCatches,
                CafeSnapshots.Select(item => item.Snapshot).ToList(),
                KeepnetRecords.ToList(),
                updated);
            Replace(FishingSessions, updated);
            OnPropertyChanged(nameof(ActiveFishingSession));
            OnPropertyChanged(nameof(FishingSessionStatus));
            OnPropertyChanged(nameof(FishingSessionCountText));
            StatusText = "Сессия рыбалки начата.";
            AppLog.Info(
                $"Начата сессия рыбалки: {session.Id}; " +
                $"водоём={session.WaterBodyName}; наживка={session.BaitName}.");
        }
        catch (Exception exception)
        {
            StatusText = $"Не удалось начать сессию: {exception.Message}";
            AppLog.Error("Не удалось начать сессию рыбалки.", exception);
        }
    }

    private void StopFishingSession()
    {
        var active = ActiveFishingSession;
        if (active is null)
        {
            StatusText = "Активная сессия отсутствует.";
            return;
        }

        var endedAt = DateTime.Now;
        var updated = FishingSessionPlanner.Stop(
            FishingSessions,
            active.Id,
            endedAt).ToList();
        try
        {
            PersistState(
                _allCatches,
                CafeSnapshots.Select(item => item.Snapshot).ToList(),
                KeepnetRecords.ToList(),
                updated);
            Replace(FishingSessions, updated);
            OnPropertyChanged(nameof(ActiveFishingSession));
            OnPropertyChanged(nameof(FishingSessionStatus));
            OnPropertyChanged(nameof(FishingSessionCountText));
            StatusText = $"Сессия завершена: {active.StartedAt:dd.MM HH:mm}–" +
                         $"{endedAt:HH:mm}.";
            AppLog.Info(
                $"Завершена сессия рыбалки: {active.Id}; " +
                $"уловов={_allCatches.Count(item =>
                    item.FishingSessionId == active.Id)}.");
        }
        catch (Exception exception)
        {
            StatusText = $"Не удалось завершить сессию: {exception.Message}";
            AppLog.Error("Не удалось завершить сессию рыбалки.", exception);
        }
    }

    private void LoadFishingSessions()
    {
        try
        {
            var sessions = _sessionStore.Load()
                .OrderByDescending(item => item.StartedAt)
                .ToList();
            Replace(FishingSessions, sessions);
            OnPropertyChanged(nameof(ActiveFishingSession));
            OnPropertyChanged(nameof(FishingSessionStatus));
            OnPropertyChanged(nameof(FishingSessionCountText));
        }
        catch (Exception exception)
        {
            AppLog.Error("Не удалось загрузить сессии рыбалки.", exception);
            StatusText = $"Не удалось загрузить сессии: {exception.Message}";
        }
    }
}
