using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Input;

public sealed class GlobalSpaceListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const uint VkSpace = 0x20;
    private const uint VkV = 0x56;
    private const uint VkM = 0x4D;
    private const uint VkC = 0x43;

    private readonly LowLevelKeyboardProc _hookProcedure;
    private readonly Channel<Action> _eventQueue =
        Channel.CreateUnbounded<Action>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
    private readonly CancellationTokenSource _dispatchCancellation = new();
    private IntPtr _hookHandle;
    private bool _spaceDown;
    private bool _vDown;
    private bool _mDown;
    private bool _cDown;
    private bool _disposed;

    public GlobalSpaceListener()
    {
        _hookProcedure = HookCallback;
        _ = DispatchEventsAsync();
        _hookHandle = SetWindowsHookEx(
            WhKeyboardLl,
            _hookProcedure,
            GetModuleHandle(null),
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public event EventHandler? SpacePressed;

    public event EventHandler? VPressed;

    public event EventHandler? MPressed;

    public event EventHandler? CPressed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventQueue.Writer.TryComplete();
        _dispatchCancellation.Cancel();
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

            if ((message == WmKeyDown || message == WmSysKeyDown) &&
                keyboardData.VirtualKeyCode == VkSpace &&
                !_spaceDown)
            {
                _spaceDown = true;
                // Space is timing-critical: RF4 can close the catch card as
                // soon as the key reaches the game. Invoke this one handler
                // before CallNextHookEx. OCR is started later by the handler
                // on the WPF dispatcher, so the hook only performs the fast
                // screen capture synchronously.
                InvokeSynchronously(SpacePressed);
            }
            else if ((message == WmKeyUp || message == WmSysKeyUp) &&
                     keyboardData.VirtualKeyCode == VkSpace)
            {
                _spaceDown = false;
            }

            if ((message == WmKeyDown || message == WmSysKeyDown) &&
                keyboardData.VirtualKeyCode == VkV &&
                !_vDown)
            {
                _vDown = true;
                QueueEvent(VPressed);
            }
            else if ((message == WmKeyUp || message == WmSysKeyUp) &&
                     keyboardData.VirtualKeyCode == VkV)
            {
                _vDown = false;
            }

            if ((message == WmKeyDown || message == WmSysKeyDown) &&
                keyboardData.VirtualKeyCode == VkM &&
                !_mDown)
            {
                _mDown = true;
                QueueEvent(MPressed);
            }
            else if ((message == WmKeyUp || message == WmSysKeyUp) &&
                     keyboardData.VirtualKeyCode == VkM)
            {
                _mDown = false;
            }

            if ((message == WmKeyDown || message == WmSysKeyDown) &&
                keyboardData.VirtualKeyCode == VkC &&
                !_cDown)
            {
                _cDown = true;
                QueueEvent(CPressed);
            }
            else if ((message == WmKeyUp || message == WmSysKeyUp) &&
                     keyboardData.VirtualKeyCode == VkC)
            {
                _cDown = false;
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private void QueueEvent(EventHandler? handler)
    {
        if (handler is null || _disposed)
        {
            return;
        }

        _eventQueue.Writer.TryWrite(
            () => handler(this, EventArgs.Empty));
    }

    private void InvokeSynchronously(EventHandler? handler)
    {
        if (handler is null || _disposed)
        {
            return;
        }

        try
        {
            handler(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            AppLog.Error(
                "Ошибка синхронного обработчика глобальной клавиши.",
                exception);
        }
    }

    private async Task DispatchEventsAsync()
    {
        try
        {
            await foreach (var action in _eventQueue.Reader.ReadAllAsync(
                               _dispatchCancellation.Token))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        "Ошибка обработчика глобальной клавиши.",
                        exception);
                }
            }
        }
        catch (OperationCanceledException)
            when (_dispatchCancellation.IsCancellationRequested)
        {
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookType,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int code,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}