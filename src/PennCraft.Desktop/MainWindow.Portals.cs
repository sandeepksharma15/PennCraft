using System.Windows.Controls;
using System.Windows.Threading;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private bool IsPortal => settings.PortalFolder is not null;
    private string[] portalPaths = [];
    private string[] DisplayPaths => IsPortal ? portalPaths : settings.ItemPaths;
    private readonly DispatcherTimer portalTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool portalRefreshBusy;

    private void InitializePortal()
    {
        AllowDrop = false;
        EmptyHint.Text = "This folder is empty";
        portalTimer.Tick += async (_, _) => await RefreshPortalAsync();
        portalTimer.Start();
        _ = RefreshPortalAsync();
    }

    private async Task RefreshPortalAsync()
    {
        if (itemsDisposed || portalRefreshBusy) return;
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

    private void ShowPortalItemMenu(PenItem item)
    {
        SelectItem(item);
        var menu = new ContextMenu { PlacementTarget = this };
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => ItemStatus.Text = ShellItemLauncher.Open(item.Path) ?? string.Empty;
        var explore = new MenuItem { Header = "Show in File Explorer" };
        explore.Click += (_, _) => ShellContextMenu.ShowInExplorer(item.Path);
        menu.Items.Add(open);
        menu.Items.Add(explore);
        menu.IsOpen = true;
    }
}
