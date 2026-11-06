using System.Windows.Controls;
using System.Windows.Threading;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal bool IsPortal => settings.PortalFolder is not null;
    private string[] portalPaths = [];
    private string[] DisplayPaths => IsPortal ? portalPaths : settings.ItemPaths;
    private readonly DispatcherTimer portalTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool portalRefreshBusy;
    private Task portalInitialLoad = Task.CompletedTask;

    private void InitializePortal()
    {
        AllowDrop = true;
        EmptyHint.Text = "This folder is empty";
        portalTimer.Tick += async (_, _) => await RefreshPortalAsync();
        portalTimer.Start();
        portalInitialLoad = RefreshPortalAsync();
    }

    private async Task RefreshPortalAsync()
    {
        if (itemsDisposed || portalRefreshBusy || transferBusy || itemMenuBusy || itemDragActive) return;
        portalRefreshBusy = true;
        try
        {
            var snapshot = await Task.Run(() => PortalDirectory.Read(settings.PortalFolder!));
            if (itemsDisposed) return;
            portalPaths = snapshot.Paths;
            RefreshItemViews();
            EmptyHint.Text = snapshot.Error is null ? "This folder is empty" : "Folder unavailable";
            ItemStatus.Text = snapshot.Error ?? string.Empty;
        }
        finally { portalRefreshBusy = false; }
    }

    internal bool ContainsDisplayedItem(string path) => DisplayPaths.Contains(path, StringComparer.OrdinalIgnoreCase);
    internal string? PortalFolder => settings.PortalFolder;
    internal bool FlushBeforePortalMove() => SaveSettings();

    internal async Task ReloadAfterPortalMoveAsync()
    {
        if (!IsPortal)
        {
            var saved = store.Load();
            CompleteTransfer(saved);
        }
        else ReleaseTransfer();
        if (IsPortal) await RefreshPortalAsync();
    }
}
