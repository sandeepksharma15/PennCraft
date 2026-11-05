using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<PenItem> items = [];
    private readonly ShellIconLoader icons = new();
    private PenMoveService? moves;
    private Point? itemDragOrigin;
    private PenItem? pressedItem;
    private bool itemDragActive;
    private bool itemsDisposed;
    private bool itemMovesBusy;
    private bool moveSettingsBlocked;

    private async Task InitializeItemsAsync()
    {
        ItemGrid.ItemsSource = items;
        itemRefreshTimer.Tick += RefreshWatchedItems;
        if (IsPortal) { InitializePortal(); return; }
        foreach (var path in settings.ItemPaths) AddItemView(path);
        UpdateEmptyHint();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
        {
            ItemStatus.Text = "Windows could not locate the user desktop. Physical moves are unavailable.";
            return;
        }
        moves = new PenMoveService(desktop, penId);
        itemMovesBusy = true;
        try
        {
            var paths = settings.ItemPaths;
            var recovery = await Task.Run(() => moves.Recover(paths));
            settings = settings with { ItemPaths = recovery.ItemPaths };
            RefreshItemViews();
            if (recovery.ResolvedIds.Length != 0)
            {
                if (SaveSettings())
                {
                    var error = await Task.Run(() => moves.Acknowledge(recovery.ResolvedIds));
                    ItemStatus.Text = error ?? "Interrupted item operations reconciled.";
                }
                else
                {
                    PauseMoves();
                }
            }
            if (recovery.Error is not null) ItemStatus.Text = recovery.Error;
            await ReconcileItemsAsync();
        }
        finally { itemMovesBusy = false; }
    }

    private void DisposeItems()
    {
        itemsDisposed = true;
        portalTimer.Stop();
        icons.Dispose();
        itemRefreshTimer.Stop();
        foreach (var watcher in itemWatchers) watcher.Dispose();
        itemWatchers.Clear();
    }

    private void AddItemView(string path)
    {
        var item = new PenItem(path) { IsSelected = string.Equals(path, selectedItemPath, StringComparison.OrdinalIgnoreCase) };
        items.Add(item);
        _ = LoadItemIconAsync(item);
    }

    private async Task LoadItemIconAsync(PenItem item)
    {
        var icon = await icons.LoadAsync(item.Path);
        if (!itemsDisposed && items.Contains(item)) item.Icon = icon;
    }

    private void RefreshItemViews()
    {
        var membership = DisplayPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(item => !membership.Contains(item.Path)).ToArray()) items.Remove(item);
        var present = items.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in DisplayPaths.Where(path => !present.Contains(path))) AddItemView(path);
        var ordered = items.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < DisplayPaths.Length; index++)
        {
            var current = items.IndexOf(ordered[DisplayPaths[index]]);
            if (current != index) items.Move(current, index);
        }
        UpdateEmptyHint();
    }

    private void UpdateEmptyHint() => EmptyHint.Visibility = items.Count == 0
        ? Visibility.Visible : Visibility.Collapsed;

    private void PreviewItemDragOver(object sender, DragEventArgs e)
    {
        // The app owns journaled moves; Explorer must never also delete the drag source.
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || transferBusy || transferRecoveryBlocked || moveSettingsBlocked || itemDragActive) return;
        try
        {
            if (PreviewInternalTransfer(e)) return;
            if (IsPortal)
            {
                e.Effects = PortalIncomingEffect(e);
                return;
            }
            if (VirtualItemDrop.IsRecycleBin(e.Data))
            {
                e.Effects = VirtualDropEffect(e.AllowedEffects);
                return;
            }
            e.Effects = ItemDragPolicy.IncomingEffect(e.AllowedEffects,
                e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false));
        }
        catch (COMException) { }
    }

    private async void DropItems(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || transferBusy || transferRecoveryBlocked || moveSettingsBlocked || itemDragActive) return;
        string[] paths;
        try
        {
            if (await DropInternalTransferAsync(e)) return;
            if (IsPortal) { await DropIntoPortalAsync(e); return; }
            if (VirtualItemDrop.IsRecycleBin(e.Data))
            {
                e.Effects = VirtualDropEffect(e.AllowedEffects);
                if (e.Effects != DragDropEffects.None)
                {
                    settings = settings with { ItemPaths = PenMembership.Normalize(settings.ItemPaths.Append(VirtualDesktopItem.RecycleBin)) };
                    RefreshItemViews();
                    if (!SaveSettings()) PauseMoves();
                }
                return;
            }
            if (ItemDragPolicy.IncomingEffect(e.AllowedEffects,
                    e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) == DragDropEffects.None
                || e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] dropped) return;
            paths = PenMembership.Normalize(dropped);
            // End the external OLE drop now. No Move effect is ever handed back to Explorer.
            e.Effects = ItemDragPolicy.IncomingEffect(e.AllowedEffects, hasFiles: true);
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            ItemStatus.Text = "Windows could not read this drop. Try dragging the item again.";
            return;
        }

        await MoveIncomingItemsAsync(paths);
    }

    internal async Task MoveIncomingItemsAsync(string[] paths, bool sharedMoveConfirmed = false)
    {
        if (!CanBeginTransfer(source: false)) return;
        var shared = paths.Where(path => SharedDesktopShortcut.Is(path)).ToArray();
        paths = paths.Where(path => !SharedDesktopShortcut.Is(path)).ToArray();
        if (shared.Length != 0)
        {
            await MoveSharedShortcutsAsync(shared, sharedMoveConfirmed);
            if (!CanBeginTransfer(false)) return;
        }
        if (paths.Length == 0) return;
        itemMovesBusy = true;
        ItemStatus.Text = "Moving desktop items into the Pen…";
        try
        {
            var outcomes = await Task.Run(() => paths.Select(path => moves?.MoveInto(path)
                ?? new MoveOutcome(path, path, null, PenMembership.Exists(path) ? null : "This item is unavailable.")).ToArray());
            var membership = settings.ItemPaths;
            var moved = 0;
            var referenced = 0;
            foreach (var outcome in outcomes.Where(outcome => outcome.Error is null))
            {
                if (outcome.Moved)
                {
                    membership = PenMembership.Normalize(membership.Select(path =>
                        string.Equals(path, outcome.Source, StringComparison.OrdinalIgnoreCase) ? outcome.Destination : path)
                        .Append(outcome.Destination));
                    moved++;
                }
                else
                {
                    membership = PenMembership.Normalize(membership.Append(outcome.Destination));
                    referenced++;
                }
            }
            settings = settings with { ItemPaths = membership };
            RefreshItemViews();
            var error = outcomes.FirstOrDefault(outcome => outcome.Error is not null)?.Error;
            await PersistItemChangesAsync(outcomes.Where(outcome => outcome.Moved).Select(outcome => outcome.OperationId!.Value));
            if (!moveSettingsBlocked)
                ItemStatus.Text = error is null
                    ? $"{moved} moved into the Pen; {referenced} reference(s) retained."
                    : $"{moved} moved. {error}";
        }
        finally { itemMovesBusy = false; }
    }

    private static DragDropEffects VirtualDropEffect(DragDropEffects allowed) =>
        (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link
        : (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;

    private void PauseMoves()
    {
        moveSettingsBlocked = true;
        moves?.BlockUntilRestart();
        ItemStatus.Text = "Settings could not be saved. Items remain at their recorded locations; restart to recover before more moves.";
    }

    private async Task PersistItemChangesAsync(IEnumerable<Guid> ids)
    {
        if (!await CaptureItemTrackingAsync())
        {
            PauseMoves();
            return;
        }
        if (moves is null) return;
        var completed = ids.ToArray();
        var error = await Task.Run(() => moves.Acknowledge(completed));
        if (error is not null)
        {
            moveSettingsBlocked = true;
            ItemStatus.Text = error;
        }
    }

    private void ItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || transferBusy || ((FrameworkElement)sender).DataContext is not PenItem item) return;
        SelectItem(item);
        pressedItem = item;
        itemDragOrigin = e.GetPosition(this);
        if (e.ClickCount == 2)
        {
            pressedItem = null;
            itemDragOrigin = null;
            ItemStatus.Text = ShellItemLauncher.Open(item.Path) ?? string.Empty;
        }
        e.Handled = true;
    }

    private void ItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        pressedItem = null;
        itemDragOrigin = null;
    }

    private async void ItemMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || transferBusy || transferRecoveryBlocked || moveSettingsBlocked || itemDragActive || pressedItem is not { } item
            || itemDragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        pressedItem = null;
        itemDragOrigin = null;
        itemDragActive = true;
        ItemStatus.Text = IsPortal ? "Drop into a Pen, another Portal or onto the desktop to move this item. Esc cancels."
            : moves?.IsManaged(item.Path) == true
            ? "Drop into another Pen or onto the desktop to move this item. Esc cancels."
            : "Drop into another Pen to transfer this reference, or onto the desktop to remove it.";
        PenDragSession? session = null;
        try
        {
            var identity = await Task.Run(() => ItemTracking.TryIdentity(item.Path));
            var recorded = settings.TrackedItems.FirstOrDefault(entry =>
                string.Equals(entry.Path, item.Path, StringComparison.OrdinalIgnoreCase))?.Identity;
            if (Mouse.LeftButton != MouseButtonState.Pressed || recorded is not null && recorded != identity)
            {
                ItemStatus.Text = "Item kept in the Pen; refresh if its source changed.";
                ScheduleItemRefresh();
                return;
            }
            session = Transfers?.Begin(penId, item.Path, identity) ?? new PenDragSession(penId, item.Path, identity);
            var result = DesktopItemDrag.Run((UIElement)sender, session, () => Transfers?.CanDropUnderCursor(session) == true);
            if (result == PenDragResult.Pen) return;
            if (result != PenDragResult.Desktop)
            {
                ItemStatus.Text = "Item kept in the Pen.";
                return;
            }
            if (IsPortal)
            {
                if (Transfers is not null) await Transfers.MovePortalItemToDesktopAsync(session);
                return;
            }
            itemMovesBusy = true;
            await MoveItemToDesktopAsync(item);
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            ItemStatus.Text = "Windows could not start the drag. The item has been kept in the Pen.";
        }
        finally
        {
            if (session is not null) Transfers?.End(session);
            itemMovesBusy = false;
            itemDragActive = false;
        }
    }
}
