using System.IO;

namespace RF4AssistantPro.Capture;

public static class GameWindowMatcher
{
    private static readonly HashSet<string> AllowedProcessNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "rf4",
            "rf4_x64",
            "russianfishing4",
            "russian fishing 4"
        };

    public static bool IsGameProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var normalized = Path.GetFileNameWithoutExtension(processName.Trim());
        return AllowedProcessNames.Contains(normalized);
    }
}