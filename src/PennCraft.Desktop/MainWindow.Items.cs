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
    private Point? itemDragOrigin;
    private PenItem? pressedItem;
    private bool itemDragActive;
    private bool itemsDisposed;

    private void InitializeItems()
    {
        ItemGrid.ItemsSource = items;
        foreach (var path in settings.ItemPaths) AddItemView(path);
        UpdateEmptyHint();
    }

    private void DisposeItems()
    {
        itemsDisposed = true;
        icons.Dispose();
    }

    private void AddItemView(string path)
    {
        var item = new PenItem(path);
        items.Add(item);
        _ = LoadItemIconAsync(item);
    }

    private async Task LoadItemIconAsync(PenItem item)
    {
        var icon = await icons.LoadAsync(item.Path);
        if (!itemsDisposed && items.Contains(item)) item.Icon = icon;
    }

    private void UpdateEmptyHint() => EmptyHint.Visibility = items.Count == 0
        ? Visibility.Visible : Visibility.Collapsed;

    private void PreviewItemDragOver(object sender, DragEventArgs e)
    {
        // Do not deserialize arbitrary formats or offer file-move semantics to Explorer.
        e.Effects = ItemDragPolicy.IncomingEffect(e.AllowedEffects,
            e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false));
        e.Handled = true;
    }

    private void DropItems(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (ItemDragPolicy.IncomingEffect(e.AllowedEffects,
                e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)) == DragDropEffects.None) return;
        try
        {
            if (e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] paths) return;
            var next = PenMembership.Add(settings.ItemPaths, paths);
            var existing = new HashSet<string>(settings.ItemPaths, StringComparer.OrdinalIgnoreCase);
            var added = next.Where(path => !existing.Contains(path)).ToArray();
            foreach (var path in added) AddItemView(path);
            settings = settings with { ItemPaths = next };
            SaveSettings();
            UpdateEmptyHint();
            ItemStatus.Text = added.Length == 0
                ? "No new available items to add."
                : $"{added.Length} item(s) added. Original desktop icons stay visible.";
            e.Effects = ItemDragPolicy.IncomingEffect(e.AllowedEffects, hasFiles: true);
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            Diagnostics.Write($"file drop failed: {exception.GetType().Name}");
            ItemStatus.Text = "Windows could not read this drop. Try dragging the item again.";
        }
    }

    private void ItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PenItem item) return;
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

    private void ItemMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (itemDragActive || pressedItem is not { } item || itemDragOrigin is not { } origin
            || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        pressedItem = null;
        itemDragOrigin = null;
        itemDragActive = true;
        ItemStatus.Text = "Drop onto exposed desktop space to remove from the Pen. Esc cancels.";
        try
        {
            if (!DesktopItemDrag.Run((UIElement)sender, item.Path))
            {
                ItemStatus.Text = "Item kept in the Pen.";
                return;
            }
            settings = settings with { ItemPaths = PenMembership.Remove(settings.ItemPaths, item.Path) };
            items.Remove(item);
            SaveSettings();
            UpdateEmptyHint();
            ItemStatus.Text = "Removed from the Pen. The original item remains on the desktop.";
        }
        catch (COMException)
        {
            ItemStatus.Text = "Windows could not start the drag. The item has been kept in the Pen.";
        }
        finally
        {
            itemDragActive = false;
        }
    }
}
