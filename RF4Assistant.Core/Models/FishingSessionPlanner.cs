namespace RF4AssistantPro.Models;

public static class FishingSessionPlanner
{
    public static IReadOnlyList<FishingSession> Start(
        IEnumerable<FishingSession> source,
        DateTime startedAt,
        string? waterBodyName,
        string? baitName)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sessions = source.ToList();
        if (sessions.Any(item => item.IsActive))
        {
            throw new InvalidOperationException(
                "Нельзя начать вторую активную сессию.");
        }

        sessions.Add(new FishingSession
        {
            StartedAt = startedAt,
            WaterBodyName = waterBodyName?.Trim() ?? "",
            BaitName = baitName?.Trim() ?? ""
        });
        return sessions;
    }

    public static IReadOnlyList<FishingSession> Stop(
        IEnumerable<FishingSession> source,
        Guid sessionId,
        DateTime endedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        var found = false;
        var sessions = source
            .Select(session =>
            {
                if (session.Id != sessionId)
                {
                    return session;
                }

                found = true;
                return session with { EndedAt = endedAt };
            })
            .ToList();

        if (!found)
        {
            throw new InvalidOperationException("Сессия не найдена.");
        }

        return sessions;
    }
}