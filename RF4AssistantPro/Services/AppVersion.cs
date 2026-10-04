namespace RF4AssistantPro.Services;

public static class AppVersion
{
    public static string Number
    {
        get
        {
            var version = typeof(AppVersion).Assembly.GetName().Version;
            return version is null
                ? "неизвестно"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string Label => $"PRO · {Number}";
}