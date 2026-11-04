using System.Runtime.InteropServices;
using System.Text;

namespace PennCraft.Desktop.Windows;

/// <summary>Read-only hit testing of exposed Explorer desktop space.</summary>
internal static class DesktopDropTarget
{
    internal static bool IsDesktop(uint shellProcess, uint targetProcess, string rootClass) =>
        shellProcess != 0 && shellProcess == targetProcess && rootClass is "Progman" or "WorkerW";

    public static bool IsUnderCursor()
    {
        var root = RootUnderCursor(); // GA_ROOT, physical screen coordinates
        if (root == 0) return false;
        Native.GetWindowThreadProcessId(Native.GetShellWindow(), out var shellProcess);
        Native.GetWindowThreadProcessId(root, out var targetProcess);
        var name = new StringBuilder(256);
        GetClassName(root, name, name.Capacity);
        return IsDesktop(shellProcess, targetProcess, name.ToString());
    }

    public static nint RootUnderCursor() => GetCursorPos(out var point)
        ? GetAncestor(WindowFromPoint(point), 2) : 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder text, int maximum);
}
