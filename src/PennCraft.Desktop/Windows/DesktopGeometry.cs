using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace PennCraft.Desktop.Windows;

[StructLayout(LayoutKind.Sequential)]
internal struct ScreenPoint(int x, int y)
{
    public int X = x;
    public int Y = y;
    public readonly Point ToPoint() => new(X, Y);
}

/// <summary>Physical-pixel shell geometry. All queries are read-only.</summary>
internal static class DesktopGeometry
{
    public static string ClassName(nint hwnd)
    {
        var text = new StringBuilder(256);
        GetClassName(hwnd, text, text.Capacity);
        return text.ToString();
    }

    public static bool IsDesktopAt(ScreenPoint point)
    {
        var root = GetAncestor(WindowFromPoint(point), 2);
        Native.GetWindowThreadProcessId(Native.GetShellWindow(), out var shellProcess);
        Native.GetWindowThreadProcessId(root, out var process);
        return DesktopDropTarget.IsDesktop(shellProcess, process, ClassName(root));
    }

    public static bool IsOwnProcessAt(ScreenPoint point)
    {
        Native.GetWindowThreadProcessId(WindowFromPoint(point), out var process);
        return process == (uint)Environment.ProcessId;
    }

    public static nint IconList()
    {
        nint found = 0;
        Native.GetWindowThreadProcessId(Native.GetShellWindow(), out var shellProcess);
        EnumWindows((hwnd, _) =>
        {
            Native.GetWindowThreadProcessId(hwnd, out var process);
            if (!DesktopDropTarget.IsDesktop(shellProcess, process, ClassName(hwnd))) return true;
            var view = FindWindowEx(hwnd, 0, "SHELLDLL_DefView", null);
            if (view != 0) found = FindWindowEx(view, 0, "SysListView32", null);
            return found == 0;
        }, 0);
        return found;
    }

    public static Rect[] Obstacles()
    {
        var result = new List<Rect>();
        Native.GetWindowThreadProcessId(Native.GetShellWindow(), out var shellProcess);
        EnumWindows((hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            Native.GetWindowThreadProcessId(hwnd, out var process);
            if (DesktopDropTarget.IsDesktop(shellProcess, process, ClassName(hwnd))) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (GetWindowRect(hwnd, out var bounds) && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
                result.Add(new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
            return true;
        }, 0);
        return [.. result];
    }

    public static double ScaleAt(ScreenPoint point)
    {
        var monitor = MonitorFromPoint(point, 2);
        return GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1;
    }

    public static void Place(Window window, Rect pixels)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (!Native.SetWindowPos(hwnd, 0, (int)pixels.Left, (int)pixels.Top, (int)pixels.Width, (int)pixels.Height,
                0x0004 | 0x0010))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool WindowCallback(nint hwnd, nint value);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint value);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(ScreenPoint point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(ScreenPoint point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
}
