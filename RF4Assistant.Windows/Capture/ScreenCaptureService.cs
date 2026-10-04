using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Capture;

public sealed class ScreenCaptureService
{
    private static readonly string[] GameTitleParts =
    [
        "Russian Fishing 4",
        "RF4"
    ];

    public GameWindowInfo? BoundGame { get; private set; }

    public GameWindowInfo? BindGame()
    {
        BoundGame = FindGameWindow();
        return BoundGame;
    }

    public bool ActivateBoundGame()
    {
        var game = GetValidBoundGame();
        if (game is null)
        {
            return false;
        }

        if (IsIconic(game.Handle))
        {
            ShowWindow(game.Handle, SwRestore);
        }

        return SetForegroundWindow(game.Handle);
    }

    public bool IsBoundGameForeground()
    {
        if (BoundGame is null)
        {
            return false;
        }

        var game = GetValidBoundGame();
        return game is not null &&
               !IsIconic(game.Handle) &&
               GetForegroundWindow() == game.Handle;
    }

    public string CaptureBoundGame(
        string filePrefix = "rf4_catch",
        bool requireForeground = true)
    {
        var game = GetValidBoundGame();
        if (game is null)
        {
            throw new InvalidOperationException(
                "Окно Russian Fishing 4 не найдено. Запусти игру и нажми «Привязать игру».");
        }

        if (IsIconic(game.Handle))
        {
            throw new InvalidOperationException(
                "Окно RF4 свёрнуто. Разверни игру и повтори снимок.");
        }

        var foregroundWindow = GetForegroundWindow();
        if (requireForeground && foregroundWindow != game.Handle)
        {
            throw new InvalidOperationException(
                "Окно RF4 не активно. Переключись в игру и нажми Space.");
        }

        if (!GetClientRect(game.Handle, out var clientRect))
        {
            throw new InvalidOperationException("Не удалось получить область окна RF4.");
        }

        var width = clientRect.Right - clientRect.Left;
        var height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Размер окна RF4 некорректен.");
        }

        var topLeft = new Point(clientRect.Left, clientRect.Top);
        if (!ClientToScreen(game.Handle, ref topLeft))
        {
            throw new InvalidOperationException("Не удалось определить координаты окна RF4.");
        }

        var directory =
            PortableDataPaths.GetScreenshotDirectory(filePrefix);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(
            directory,
            $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            topLeft,
            Point.Empty,
            bitmap.Size,
            CopyPixelOperation.SourceCopy);
        bitmap.Save(filePath, ImageFormat.Png);

        return filePath;
    }

    private GameWindowInfo? GetValidBoundGame()
    {
        if (BoundGame is not null &&
            IsWindow(BoundGame.Handle) &&
            IsWindowVisible(BoundGame.Handle) &&
            GameWindowMatcher.IsGameProcess(
                GetWindowProcessName(BoundGame.Handle)))
        {
            return BoundGame;
        }

        BoundGame = FindGameWindow();
        return BoundGame;
    }

    private static GameWindowInfo? FindGameWindow()
    {
        GameWindowInfo? result = null;
        EnumWindows(
            (handle, _) =>
            {
                if (!IsWindowVisible(handle))
                {
                    return true;
                }

                var title = GetWindowTitle(handle);
                if (title.Length == 0 ||
                    title.Contains("Assistant", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!GameTitleParts.Any(part =>
                        title.Contains(part, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                var processName = GetWindowProcessName(handle);
                if (GameWindowMatcher.IsGameProcess(processName))
                {
                    result = new GameWindowInfo(handle, title, processName);
                    return false;
                }

                return true;
            },
            IntPtr.Zero);

        return result;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return "";
        }

        var builder = new StringBuilder(length + 1);
        GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetWindowProcessName(IntPtr handle)
    {
        try
        {
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0)
            {
                return "";
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr handle,
        out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr handle, int command);

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr handle, out Rect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr handle, ref Point point);

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public sealed record GameWindowInfo(
    IntPtr Handle,
    string Title,
    string ProcessName);