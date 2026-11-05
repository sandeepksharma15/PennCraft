using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PennCraft.Desktop.Windows;

/// <summary>Independent top-level HWND; never parents to or modifies an Explorer window.</summary>
internal sealed class DesktopSurface : IDisposable
{
    private static readonly System.Threading.Lock BandGate = new();
    private static readonly List<DesktopSurface> Surfaces = [];
    private readonly nint hwnd;
    private readonly HwndSource source;
    private bool editing;
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
        lock (BandGate) Surfaces.Add(this);
        Diagnostics.Write($"start OS={Environment.OSVersion} runtime={Environment.Version} hwnd={hwnd:X}");
    }

    private nint Hook(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021 && !editing) // WM_MOUSEACTIVATE
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

    public void SetEditing(bool value)
    {
        editing = value;
        var style = Native.GetWindowLongPtr(hwnd, -20);
        Native.SetWindowLongPtr(hwnd, -20, value ? style & ~0x08000000 : style | 0x08000000);
    }

    public void Refresh(bool userVisible = true)
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

        var show = DesktopPolicy.ShouldShow(shell != 0 && shellProcess != 0, userVisible);
        if (!show)
        {
            if (Native.IsWindowVisible(hwnd)) Native.ShowWindow(hwnd, 0);
        }
        else
        {
            // Restore Show Desktop hides, but do not rotate overlapping Pens on every timer tick.
            if (Native.IsIconic(hwnd) || !Native.IsWindowVisible(hwnd)) Native.ShowWindow(hwnd, 4);
            if (!RepairBand())
            {
                Diagnostics.Write($"Pen band placement failed error={Marshal.GetLastWin32Error()}");
                Native.ShowWindow(hwnd, 0);
                visible = null;
                return;
            }
        }

        if (visible != show) Diagnostics.Write($"visible={show} userVisible={userVisible}");
        visible = show;
    }

    private static bool RepairBand()
    {
        lock (BandGate)
        {
            var active = Surfaces.Where(surface => Native.IsWindowVisible(surface.hwnd) && !Native.IsIconic(surface.hwnd))
                .Reverse().ToArray(); // Newer Pens remain above older Pens.
            if (active.Length == 0) return true;
            List<DesktopBandWindow> windows = [];
            HashSet<nint> visited = [];
            for (var window = Native.GetTopWindow(0); window != 0 && visited.Add(window); window = Native.GetWindow(window, 2))
            {
                if (!Native.IsWindowVisible(window) || Native.IsIconic(window)) continue;
                var name = new System.Text.StringBuilder(256);
                Native.GetClassName(window, name, name.Capacity);
                windows.Add(new(window, name.ToString() is "Progman" or "WorkerW"));
            }
            if (!DesktopBandPolicy.NeedsRepair([.. active.Select(surface => surface.hwnd)], [.. windows])) return true;
            // Commit all changes together so repair itself cannot expose intermediate overlap order.
            var batch = Native.BeginDeferWindowPos(active.Length);
            if (batch == 0) return false;
            foreach (var surface in active)
            {
                batch = Native.DeferWindowPos(batch, surface.hwnd, 1, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0200);
                if (batch == 0) return false;
            }
            return Native.EndDeferWindowPos(batch);
        }
    }

    public void Dispose()
    {
        lock (BandGate) Surfaces.Remove(this);
        source.RemoveHook(Hook);
        Diagnostics.Write("stop");
    }
}

internal static partial class Native
{
    [LibraryImport("user32.dll")]
    internal static partial nint GetTopWindow(nint hwnd);
    [LibraryImport("user32.dll")]
    internal static partial nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint hwnd, System.Text.StringBuilder text, int maximum);
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint BeginDeferWindowPos(int count);
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint DeferWindowPos(nint batch, nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EndDeferWindowPos(nint batch);
    [LibraryImport("user32.dll")]
    internal static partial nint GetParent(nint hwnd);
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();
    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hwnd);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hwnd);
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string message);
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int command);
    [LibraryImport("kernel32.dll")]
    internal static partial void SetLastError(uint error);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetLong64(nint hwnd, int index);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetLong32(nint hwnd, int index);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetLong64(nint hwnd, int index, nint value);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetLong32(nint hwnd, int index, int value);
    internal static nint GetWindowLongPtr(nint hwnd, int index) => nint.Size == 8 ? GetLong64(hwnd, index) : GetLong32(hwnd, index);
    internal static nint SetWindowLongPtr(nint hwnd, int index, nint value) => nint.Size == 8 ? SetLong64(hwnd, index, value) : SetLong32(hwnd, index, (int)value);
}
