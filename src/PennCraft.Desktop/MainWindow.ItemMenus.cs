using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private readonly List<FileSystemWatcher> itemWatchers = [];
    private HashSet<string> watchedMembers = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer itemRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool itemMenuBusy;
    private bool itemReconciliationBusy;
    private string? selectedItemPath;

    private void SelectItem(PenItem? selected)
    {
        selectedItemPath = selected?.Path;
        foreach (var item in items) item.IsSelected = ReferenceEquals(item, selected);
    }

    private async void ItemRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not PenItem item) return;
        await ShowItemMenuAsync(item, ((FrameworkElement)sender).PointToScreen(e.GetPosition((IInputElement)sender)));
    }

    private async void ItemContextKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Apps && !(e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) return;
        var selected = items.FirstOrDefault(item => item.IsSelected);
        if (selected is null) return;
        e.Handled = true;
        var element = ItemGrid.ItemContainerGenerator.ContainerFromItem(selected) as FrameworkElement;
        await ShowItemMenuAsync(selected, (element ?? this).PointToScreen(new Point(24, 24)));
    }

    private async Task ShowItemMenuAsync(PenItem item, Point pixels)
    {
        if (itemMovesBusy || itemDragActive || itemMenuBusy || itemReconciliationBusy || transferBusy || transferRecoveryBlocked || moveSettingsBlocked
            || moves?.HasUnresolvedMoves == true)
        {
            ItemStatus.Text = "Finish or recover the pending item operation before using item commands.";
            return;
        }
        SelectItem(item);
        pressedItem = null;
        itemDragOrigin = null;
        if (!PenMembership.Exists(item.Path))
        {
            if (IsPortal) ItemStatus.Text = "This item is unavailable. Refreshing the folder.";
            else ShowUnavailableMenu(item);
            return;
        }
        itemMenuBusy = true;
        FinishEditing(commit: true);
        surface?.SetEditing(true);
        Activate();
        try
        {
            var clickedIdentity = await Task.Run(() => ItemTracking.TryIdentity(item.Path));
            using var menu = new ShellContextMenu(item.Path, new WindowInteropHelper(this).Handle,
                moves?.IsManaged(item.Path) == true, includeMembership: !IsPortal);
            var choice = menu.Show(pixels);
            if (choice.Action == ItemMenuAction.Cancel) return;
            if (choice.Action == ItemMenuAction.ShowInExplorer)
            {
                ShellContextMenu.ShowInExplorer(item.Path);
                return;
            }
            if (choice.Action == ItemMenuAction.RemoveReference)
            {
                await RemoveReferenceAsync(item);
                return;
            }
            if (VirtualDesktopItem.IsRecycleBin(item.Path))
            {
                if (choice.Action == ItemMenuAction.ShellCommand) menu.Invoke(choice.CommandId);
                else ItemStatus.Text = "Use the Windows item commands for Recycle Bin.";
                return;
            }
            if (clickedIdentity is null
                || await Task.Run(() => ItemTracking.TryIdentity(item.Path)) != clickedIdentity
                || settings.TrackedItems.Any(entry => string.Equals(entry.Path, item.Path, StringComparison.OrdinalIgnoreCase)
                    && entry.Identity != clickedIdentity))
            {
                ItemStatus.Text = "The item changed or could not be identified reliably. Refreshing the Pen; try again.";
                await ReconcileItemsAsync();
                return;
            }
            if (choice.Action == ItemMenuAction.MoveToDesktop)
            {
                await MoveItemToDesktopAsync(item);
                return;
            }

            // Save identity before allowing Windows or our rename dialog to change the filesystem.
            if (!IsPortal && !await CaptureItemTrackingAsync())
            {
                PauseMoves();
                return;
            }
            if (!IsPortal && !settings.TrackedItems.Any(entry => string.Equals(entry.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
            {
                ItemStatus.Text = "Windows could not identify this item reliably. Use File Explorer for this command.";
                return;
            }
            if (choice.Action == ItemMenuAction.Rename)
            {
                var dialog = new RenameItemDialog(this, item.Path);
                if (dialog.ShowDialog() == true)
                {
                    var name = dialog.ItemName;
                    var error = await Task.Run(() => ItemRename.Rename(item.Path, name, clickedIdentity));
                    ItemStatus.Text = error ?? string.Empty;
                }
            }
            else if (await Task.Run(() => ItemTracking.TryIdentity(item.Path)) == clickedIdentity)
                menu.Invoke(choice.CommandId);
            else ItemStatus.Text = "The item changed before the command could run. Refreshing the Pen.";
            if (!IsPortal) await ReconcileItemsAsync();
        }
        catch (COMException exception) when (exception.HResult == unchecked((int)0x800704C7))
        {
            ItemStatus.Text = string.Empty; // Windows operation cancelled by the user.
            ScheduleItemRefresh();
        }
        catch (Exception exception) when (exception is COMException or Win32Exception or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            Diagnostics.Write($"item context command failed: {exception.GetType().Name}");
            ItemStatus.Text = "Windows could not display or complete that item command. The file was left to Windows to manage.";
            ScheduleItemRefresh();
        }
        finally
        {
            surface?.SetEditing(editing);
            itemMenuBusy = false;
            if (IsPortal) await RefreshPortalAsync();
        }
    }

    private void ShowUnavailableMenu(PenItem item)
    {
        var menu = new ContextMenu { PlacementTarget = this };
        var remove = new MenuItem { Header = "Remove unavailable entry from Pen" };
        remove.Click += async (_, _) => await RemoveReferenceAsync(item);
        menu.Items.Add(remove);
        var parent = Path.GetDirectoryName(item.Path);
        if (parent is not null && Directory.Exists(parent))
        {
            var explore = new MenuItem { Header = "Open containing folder" };
            explore.Click += (_, _) => ItemStatus.Text = ShellItemLauncher.Open(parent) ?? string.Empty;
            menu.Items.Add(explore);
        }
        menu.IsOpen = true;
    }

    private async Task RemoveReferenceAsync(PenItem item)
    {
        var wasBusy = itemMenuBusy;
        itemMenuBusy = true;
        try
        {
            // Membership only: no file deletion, even for an unavailable stored item.
            settings = settings with { ItemPaths = PenMembership.Remove(settings.ItemPaths, item.Path) };
            SelectItem(null);
            RefreshItemViews();
            await PersistItemChangesAsync([]);
            if (!moveSettingsBlocked) ItemStatus.Text = "Removed from the Pen; the filesystem item was not deleted.";
        }
        finally { itemMenuBusy = wasBusy; }
    }

    private async Task MoveItemToDesktopAsync(PenItem item)
    {
        var outcome = await Task.Run(() => moves?.MoveOut(item.Path) ?? new MoveOutcome(item.Path, item.Path, null, null));
        if (outcome.Error is not null) { ItemStatus.Text = outcome.Error; return; }
        settings = settings with { ItemPaths = PenMembership.Remove(settings.ItemPaths, item.Path) };
        SelectItem(null);
        RefreshItemViews();
        await PersistItemChangesAsync(outcome.OperationId.HasValue ? [outcome.OperationId.Value] : []);
        if (!moveSettingsBlocked)
            ItemStatus.Text = outcome.Moved ? "Moved back to the desktop." : "Reference removed; its original item was not moved.";
    }

    private async Task<bool> CaptureItemTrackingAsync()
    {
        var paths = settings.ItemPaths;
        var previous = settings.TrackedItems;
        var tracking = await Task.Run(() => ItemTracking.Capture(paths, previous));
        if (itemsDisposed) return false;
        settings = settings with { TrackedItems = tracking };
        if (!SaveSettings()) return false;
        RestartItemWatchers();
        return true;
    }

    private async Task ReconcileItemsAsync()
    {
        if (IsPortal || itemsDisposed || itemReconciliationBusy || transferBusy || transferRecoveryBlocked || moves?.HasUnresolvedMoves == true) return;
        itemReconciliationBusy = true;
        try
        {
            var paths = settings.ItemPaths;
            var previous = settings.TrackedItems;
            var selectedIdentity = previous.FirstOrDefault(entry =>
                string.Equals(entry.Path, selectedItemPath, StringComparison.OrdinalIgnoreCase))?.Identity;
            var reconciled = await Task.Run(() => ItemTracking.Reconcile(paths, previous));
            if (itemsDisposed) return;
            var captured = await Task.Run(() => ItemTracking.Capture(reconciled.Paths, reconciled.Tracking));
            if (selectedIdentity is not null)
                selectedItemPath = captured.FirstOrDefault(entry => entry.Identity == selectedIdentity)?.Path;
            settings = settings with { ItemPaths = reconciled.Paths, TrackedItems = captured };
            RefreshItemViews();
            SelectItem(items.FirstOrDefault(item => string.Equals(item.Path, selectedItemPath, StringComparison.OrdinalIgnoreCase)));
            if (!SaveSettings()) PauseMoves();
            RestartItemWatchers();
            if (reconciled.Uncertain) ItemStatus.Text = "Some unavailable or ambiguous items were retained for a later refresh.";
        }
        finally { itemReconciliationBusy = false; }
    }

    private void RestartItemWatchers()
    {
        Volatile.Write(ref watchedMembers, settings.ItemPaths.ToHashSet(StringComparer.OrdinalIgnoreCase));
        var parents = settings.ItemPaths.Where(path => !VirtualDesktopItem.IsRecycleBin(path)).Select(Path.GetDirectoryName).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var watcher in itemWatchers.ToArray())
        {
            if (parents.Contains(watcher.Path) && watcher.EnableRaisingEvents) continue;
            watcher.Dispose();
            itemWatchers.Remove(watcher);
        }
        foreach (var parent in parents)
        {
            if (itemWatchers.Any(watcher => string.Equals(watcher.Path, parent, StringComparison.OrdinalIgnoreCase))) continue;
            try
            {
                if (!Directory.Exists(parent)) continue;
                var watcher = new FileSystemWatcher(parent)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false
                };
                watcher.Renamed += (_, e) =>
                {
                    if (ItemWatchPolicy.IsRelevant(Volatile.Read(ref watchedMembers), e.FullPath, e.OldFullPath))
                        QueueItemRefresh();
                };
                watcher.Deleted += (_, e) =>
                {
                    if (ItemWatchPolicy.IsRelevant(Volatile.Read(ref watchedMembers), e.FullPath)) QueueItemRefresh();
                };
                watcher.Created += (_, e) =>
                {
                    if (ItemWatchPolicy.IsRelevant(Volatile.Read(ref watchedMembers), e.FullPath)) QueueItemRefresh();
                };
                watcher.Error += (_, _) => QueueItemRefresh();
                watcher.EnableRaisingEvents = true;
                itemWatchers.Add(watcher);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Diagnostics.Write($"item watcher unavailable: {exception.GetType().Name}");
            }
        }
    }

    private void QueueItemRefresh()
    {
        if (!itemsDisposed && !Dispatcher.HasShutdownStarted) Dispatcher.InvokeAsync(ScheduleItemRefresh);
    }

    private void ScheduleItemRefresh()
    {
        if (itemsDisposed) return;
        itemRefreshTimer.Stop();
        itemRefreshTimer.Start();
    }

    private async void RefreshWatchedItems(object? sender, EventArgs e)
    {
        itemRefreshTimer.Stop();
        if (itemMovesBusy || itemDragActive || itemMenuBusy || itemReconciliationBusy || transferBusy)
        {
            itemRefreshTimer.Start();
            return;
        }
        await ReconcileItemsAsync();
    }

    private void PenBackgroundRightClick(object sender, MouseButtonEventArgs e)
    {
        if (TitleEditor.IsMouseOver || itemMovesBusy || itemMenuBusy || transferBusy) return;
        e.Handled = true;
        SelectItem(null);
        ShowPenMenu(this);
    }
}
