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

    private async void InitializeItems()
    {
        ItemGrid.ItemsSource = items;
        itemRefreshTimer.Tick += RefreshWatchedItems;
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
        var membership = settings.ItemPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(item => !membership.Contains(item.Path)).ToArray()) items.Remove(item);
        var present = items.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in settings.ItemPaths.Where(path => !present.Contains(path))) AddItemView(path);
        var ordered = items.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.ItemPaths.Length; index++)
        {
            var current = items.IndexOf(ordered[settings.ItemPaths[index]]);
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
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || moveSettingsBlocked || itemDragActive) return;
        try
        {
            e.Effects = ItemDragPolicy.IncomingEffect(e.AllowedEffects,
                e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false));
        }
        catch (COMException) { }
    }

    private async void DropItems(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || moveSettingsBlocked || itemDragActive) return;
        string[] paths;
        try
        {
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
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || ((FrameworkElement)sender).DataContext is not PenItem item) return;
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
        if (itemMovesBusy || itemMenuBusy || itemReconciliationBusy || moveSettingsBlocked || itemDragActive || pressedItem is not { } item
            || itemDragOrigin is not { } origin || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        pressedItem = null;
        itemDragOrigin = null;
        itemDragActive = true;
        ItemStatus.Text = moves?.IsManaged(item.Path) == true
            ? "Drop onto exposed desktop space to move this item back. Esc cancels."
            : "Drop onto exposed desktop space to remove this reference. Esc cancels.";
        try
        {
            if (!DesktopItemDrag.Run((UIElement)sender, item.Path))
            {
                ItemStatus.Text = "Item kept in the Pen.";
                return;
            }
            itemMovesBusy = true;
            await MoveItemToDesktopAsync(item);
        }
        catch (COMException)
        {
            ItemStatus.Text = "Windows could not start the drag. The item has been kept in the Pen.";
        }
        finally
        {
            itemMovesBusy = false;
            itemDragActive = false;
        }
    }
}
