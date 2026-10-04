using System.IO;
using System.Security.Cryptography;

namespace RF4AssistantPro.Services;

public static class RecordIdentityService
{
    public static string TryComputeScreenshotHash(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "";
        }

        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(
                SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception)
        {
            AppLog.Warn(
                $"Не удалось вычислить хеш снимка «{path}»: " +
                exception.Message);
            return "";
        }
    }
}