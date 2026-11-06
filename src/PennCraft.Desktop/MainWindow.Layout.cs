using System.Windows;
using System.Windows.Interop;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal Action? ArrangeRequested { get; set; }
    internal Action? UndoLayoutRequested { get; set; }
    internal Func<bool>? CanUndoLayout { get; set; }
    internal Task LayoutReady => IsPortal ? portalInitialLoad : ItemsReady;
    internal bool CanArrange => CanExit && !portalRefreshBusy && !transferRecoveryBlocked && !moveSettingsBlocked
        && ItemsReady.IsCompletedSuccessfully && moves?.HasUnresolvedMoves != true;
    private bool resumeItemRefresh;
    private bool resumeSave;

    internal PenLayoutItem MeasureLayoutItem(double width)
    {
        RefreshSurface();
        ItemStatus.Measure(new Size(Math.Max(0, width - 20), double.PositiveInfinity));
        var chrome = Math.Max(32, Header.ActualHeight) + 12 + Math.Ceiling(ItemStatus.DesiredSize.Height);
        return new(penId, DesktopGeometry.Bounds(this), DisplayPaths.Length, chrome);
    }

    internal void BeginLayout()
    {
        FinishEditing(commit: true);
        resumeSave = saveTimer.IsEnabled;
        resumeItemRefresh = itemRefreshTimer.IsEnabled;
        saveTimer.Stop();
        itemRefreshTimer.Stop();
        transferBusy = true;
    }

    internal void EndLayout()
    {
        transferBusy = false;
        saveTimer.Stop();
        if (resumeSave) saveTimer.Start();
        if (resumeItemRefresh) itemRefreshTimer.Start();
    }

    internal void ApplyLayoutBounds(Rect pixels)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        // Move to the destination monitor first so WPF processes its DPI transition before final sizing.
        if (!Native.SetWindowPos(hwnd, 0, (int)pixels.Left, (int)pixels.Top, 0, 0, 0x0001 | 0x0004 | 0x0010))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        DesktopGeometry.Place(this, pixels);
        UpdateLayout();
        settings = settings with { Left = Left, Top = Top, Width = Width, Height = Height };
    }
}
