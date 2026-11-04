using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace PennCraft.Desktop.Windows;

/// <summary>Observation only: every mouse event is forwarded, never captured or suppressed.</summary>
internal sealed class DesktopCreationGesture : IDisposable
{
    private readonly Dispatcher ui;
    private readonly Action<Rect> offer;
    private readonly Action dismiss;
    private static readonly int[] ModifierKeys = [16, 17, 18, 91, 92, 27];
    private ScreenPoint lastPoint;
    private readonly BlockingCollection<Candidate> candidates = new(1);
    private readonly Thread hookThread;
    private readonly Thread automationThread;
    private readonly MouseCallback callback;
    private Dispatcher? hookDispatcher;
    private DesktopIconSnapshot? snapshot;
    private Candidate? drawing;
    private nint hook;
    private long serial;
    private volatile bool enabled = true;
    private volatile bool disposed;

    public bool Enabled
    {
        get => enabled;
        set { enabled = value; Interlocked.Increment(ref serial); }
    }

    public DesktopCreationGesture(Dispatcher ui, Action<Rect> offer, Action dismiss)
    {
        this.ui = ui;
        this.offer = offer;
        this.dismiss = dismiss;
        callback = Observe;
        hookThread = new Thread(HookLoop) { IsBackground = true, Name = "PennCraft desktop observer" };
        hookThread.SetApartmentState(ApartmentState.STA);
        automationThread = new Thread(AutomationLoop) { IsBackground = true, Name = "PennCraft desktop geometry" };
        automationThread.SetApartmentState(ApartmentState.MTA);
        automationThread.Start();
        hookThread.Start();
    }

    private void HookLoop()
    {
        SetThreadDpiAwarenessContext(-4);
        hookDispatcher = Dispatcher.CurrentDispatcher;
        try
        {
            hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
            if (hook == 0)
            {
                Diagnostics.Write($"desktop observer unavailable error={Marshal.GetLastWin32Error()}");
                return;
            }
            var escapeDown = false;
            var escape = new DispatcherTimer(TimeSpan.FromMilliseconds(25), DispatcherPriority.Send, (_, _) =>
            {
                var down = (GetAsyncKeyState(27) & 0x8000) != 0;
                if (down && !escapeDown)
                {
                    drawing = null;
                    Interlocked.Increment(ref serial);
                    ui.BeginInvoke(dismiss);
                }
                escapeDown = down;
            }, hookDispatcher);
            if (!disposed) Dispatcher.Run();
            escape.Stop();
        }
        finally
        {
            if (hook != 0) UnhookWindowsHookEx(hook);
            GC.KeepAlive(callback);
        }
    }

    private nint Observe(int code, nint message, nint data)
    {
        if (code >= 0 && !disposed)
        {
            var msg = unchecked((int)message);
            var mouse = Marshal.PtrToStructure<MouseData>(data);
            var moved = mouse.Point.X != lastPoint.X || mouse.Point.Y != lastPoint.Y;
            var sequence = msg != 0x0200 || moved ? Interlocked.Increment(ref serial) : Interlocked.Read(ref serial);
            lastPoint = mouse.Point;
            if (msg is 0x0201 or 0x0204 or 0x0207 && !DesktopGeometry.IsOwnProcessAt(mouse.Point))
                ui.BeginInvoke(dismiss);
            if (!enabled || (mouse.Flags & 1) != 0 || ModifiersPressed()) drawing = null;
            else if (msg == 0x0201)
            {
                var cached = Volatile.Read(ref snapshot);
                drawing = cached is { IsFresh: true } && DesktopGeometry.IsDesktopAt(mouse.Point)
                    && !cached.Icons.Any(icon => icon.Contains(mouse.Point.ToPoint()))
                    ? new(mouse.Point, mouse.Point, cached, sequence) : null;
            }
            else if (message == 0x0200 && drawing is not null && !DesktopGeometry.IsDesktopAt(mouse.Point))
                drawing = null;
            else if (message == 0x0202)
            {
                if (drawing is { } candidate && DesktopGeometry.IsDesktopAt(mouse.Point))
                    candidates.TryAdd(candidate with { End = mouse.Point, Serial = sequence });
                drawing = null;
            }
            else if (message is 0x0204 or 0x0207 or 0x020B or 0x020A)
                drawing = null;
        }
        return CallNextHookEx(hook, code, message, data);
    }

    private static bool ModifiersPressed() => ModifierKeys
        .Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);

    private void AutomationLoop()
    {
        SetThreadDpiAwarenessContext(-4);
        try
        {
            while (!disposed)
            {
                Volatile.Write(ref snapshot, enabled ? DesktopIconSnapshot.Read() : null);
                if (!candidates.TryTake(out var candidate, 600)) continue;
                Thread.Sleep(80);
                if (disposed || !enabled || candidate.Serial != Interlocked.Read(ref serial)) continue;
                var current = DesktopIconSnapshot.Read();
                var region = CreationGesturePolicy.Rectangle(candidate.Start.ToPoint(), candidate.End.ToPoint());
                if (current is null || !candidate.Snapshot.SameLayout(current)
                    || !DesktopGeometry.IsDesktopAt(candidate.Start) || !DesktopGeometry.IsDesktopAt(candidate.End)
                    || !CreationGesturePolicy.CanCreate(region, candidate.Start.ToPoint(),
                        DesktopGeometry.ScaleAt(new ScreenPoint((int)region.Left, (int)region.Top)), current.IsFresh, current.HasSelection,
                        current.Icons, DesktopGeometry.Obstacles())) continue;
                ui.BeginInvoke(() =>
                {
                    if (!disposed && enabled && candidate.Serial == Interlocked.Read(ref serial))
                        offer(region);
                });
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            Diagnostics.Write($"desktop geometry observer stopped: {exception.GetType().Name}");
        }
        finally { enabled = false; }
    }

    public void Dispose()
    {
        disposed = true;
        enabled = false;
        Interlocked.Increment(ref serial);
        hookDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
    }

    private sealed record Candidate(ScreenPoint Start, ScreenPoint End, DesktopIconSnapshot Snapshot, long Serial);
    private delegate nint MouseCallback(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData { public ScreenPoint Point; public uint Mouse, Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int type, MouseCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
}
