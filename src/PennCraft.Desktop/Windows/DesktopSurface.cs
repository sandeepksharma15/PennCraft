using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace PennCraft.Desktop.Windows;

/// <summary>Independent top-level HWND; never parents to or modifies an Explorer window.</summary>
internal sealed class DesktopSurface : IDisposable
{
    private readonly nint hwnd;
    private readonly HwndSource source;
    private nint lastShell;
    private uint lastProcess;
    private bool? visible;
    private readonly uint taskbarCreated;

    public DesktopSurface(nint hwnd)
    {
        this.hwnd = hwnd;
        source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Missing WPF HWND.");
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        source.AddHook(Hook);
        // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; remove WS_EX_APPWINDOW.
        var style = Native.GetWindowLongPtr(hwnd, -20);
        Native.SetLastError(0);
        if (Native.SetWindowLongPtr(hwnd, -20, (style | 0x08000080) & ~0x00040000) == 0
            && Marshal.GetLastWin32Error() != 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        Diagnostics.Write($"start OS={Environment.OSVersion} runtime={Environment.Version} hwnd={hwnd:X}");
    }

    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) // WM_MOUSEACTIVATE
        {
            handled = true;
            return 3; // MA_NOACTIVATE: retain click, preserve desktop focus.
        }
        if ((uint)message == taskbarCreated)
        {
            visible = null;
            Diagnostics.Write("TaskbarCreated; rediscover shell");
        }
        return 0;
    }

    public void InvalidateVisibility() => visible = null;

    public void Refresh()
    {
        var shell = Native.GetShellWindow();
        Native.GetWindowThreadProcessId(shell, out var shellProcess);
        if (shell != lastShell || shellProcess != lastProcess)
        {
            Diagnostics.Write($"shell hwnd={shell:X} pid={shellProcess}; previous={lastShell:X}/{lastProcess}");
            lastShell = shell;
            lastProcess = shellProcess;
            visible = null;
        }
        var foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out var foregroundProcess);
        var name = new StringBuilder(256);
        Native.GetClassName(foreground, name, name.Capacity);
        var desktop = foregroundProcess == shellProcess &&
            (name.ToString() is "Progman" or "WorkerW");
        var show = DesktopPolicy.ShouldShow(shell != 0 && shellProcess != 0, desktop);
        if (visible == show) return;
        if (show)
        {
            // Bottom of ordinary top-level windows; never topmost, never activate.
            if (!Native.SetWindowPos(hwnd, 1, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0040))
            {
                Diagnostics.Write($"SetWindowPos failed error={Marshal.GetLastWin32Error()}");
                Native.ShowWindow(hwnd, 0);
                visible = null; // retry on next poll
                return;
            }
        }
        else Native.ShowWindow(hwnd, 0);
        visible = show;
        Diagnostics.Write($"visible={show} foreground={foreground:X} class={name} pid={foregroundProcess}");
    }

    public void Dispose()
    {
        source.RemoveHook(Hook);
        Diagnostics.Write("stop");
    }
}

internal static class Native
{
    [DllImport("user32.dll")] internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern nint GetShellWindow();
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("kernel32.dll")] internal static extern void SetLastError(uint error);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetLong64(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetLong32(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetLong64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetLong32(nint hwnd, int index, int value);
    internal static nint GetWindowLongPtr(nint hwnd, int index) => nint.Size == 8 ? GetLong64(hwnd, index) : GetLong32(hwnd, index);
    internal static nint SetWindowLongPtr(nint hwnd, int index, nint value) => nint.Size == 8 ? SetLong64(hwnd, index, value) : SetLong32(hwnd, index, (int)value);
}
