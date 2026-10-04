using System.IO;

namespace RF4AssistantPro.Services;

public static class PortableDataPaths
{
    public static string BaseDirectory { get; } =
        AppContext.BaseDirectory;

    public static string LogsDirectory { get; } =
        Path.Combine(BaseDirectory, "Logs");

    public static string ScreenshotsDirectory { get; } =
        Path.Combine(BaseDirectory, "Screenshots");

    public static string ScreenshotSettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RF4AssistantPro",
        "screenshot-retention.json");

    public static string GetScreenshotDirectory(string filePrefix)
    {
        var folderName = filePrefix switch
        {
            var value when value.StartsWith(
                "rf4_catch",
                StringComparison.OrdinalIgnoreCase) => "Space_Catches",
            var value when value.StartsWith(
                "rf4_waterbody",
                StringComparison.OrdinalIgnoreCase) => "M_WaterBody",
            var value when value.StartsWith(
                "rf4_bait",
                StringComparison.OrdinalIgnoreCase) => "V_Bait",
            var value when value.StartsWith(
                "rf4_keepnet",
                StringComparison.OrdinalIgnoreCase) => "C_Keepnet",
            var value when value.StartsWith(
                "rf4_cafe",
                StringComparison.OrdinalIgnoreCase) => "Cafe",
            _ => "Other"
        };

        return Path.Combine(ScreenshotsDirectory, folderName);
    }
}