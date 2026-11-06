using System.Windows;
using System.Windows.Controls;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private void OpenMenu(object sender, RoutedEventArgs e) => ShowPenMenu((FrameworkElement)sender);

    private void ShowPenMenu(FrameworkElement target)
    {
        if (deleted) return;
        var menu = BuildPenMenu();
        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    internal ContextMenu BuildPenMenu()
    {
        MenuItem Command(string title, Action action, Func<bool>? available = null)
        {
            bool CanRun() => CanChangePresentation && (available?.Invoke() ?? true);
            var command = new MenuItem { Header = title, IsEnabled = CanRun() };
            command.Click += (_, _) => { if (CanRun()) action(); };
            return command;
        }

        var menu = new ContextMenu();
        menu.Items.Add(Command("Rename Pen", () => Dispatcher.InvokeAsync(() =>
        {
            if (CanChangePresentation) BeginEditing();
        })));

        var view = new MenuItem { Header = "View" };
        view.Items.Add(Command(settings.IsFolded ? "Unfold Pen" : "Fold Pen", () => ToggleFold()));
        var sizes = new MenuItem { Header = "Item size" };
        foreach (var value in PenItemScale.Values)
        {
            var size = Command(value == 1 ? "100% (default)" : $"{value:P0}", () => SetItemScale(value));
            size.IsCheckable = true;
            size.IsChecked = settings.ItemScale == value;
            sizes.Items.Add(size);
        }
        view.Items.Add(sizes);
        var opacity = new MenuItem { Header = "Background opacity" };
        foreach (var value in new[] { 0.35, 0.55, 0.78, 1.0 })
        {
            var option = Command($"{value:P0}", () => SetBackgroundOpacity(value));
            option.IsCheckable = true;
            option.IsChecked = Math.Abs(settings.BackgroundOpacity - value) < 0.01;
            opacity.Items.Add(option);
        }
        view.Items.Add(opacity);
        menu.Items.Add(view);

        var layout = new MenuItem { Header = "Layout" };
        layout.Items.Add(Command("Arrange Pens automatically", () => ArrangeRequested?.Invoke(), () => CanArrange));
        layout.Items.Add(Command("Undo last automatic layout", () => UndoLayoutRequested?.Invoke(), () => CanUndoLayout?.Invoke() == true));
        layout.Items.Add(Command("Reset position and show", ResetPosition));
        layout.Items.Add(new Separator());
        layout.Items.Add(Command("Save current layout…", () => SaveLayoutRequested?.Invoke()));
        layout.Items.Add(Command("Saved layouts…", () => SavedLayoutsRequested?.Invoke()));
        layout.Items.Add(Command("Undo last layout restore", () => UndoSavedLayoutRequested?.Invoke(), () => CanUndoSavedLayout?.Invoke() == true));
        menu.Items.Add(layout);
        menu.Items.Add(new Separator());

        if (!IsPortal)
        {
            menu.Items.Add(Command("Add Recycle Bin", () =>
            {
                settings = settings with
                {
                    ItemPaths = Items.PenMembership.Normalize(settings.ItemPaths.Append(Items.VirtualDesktopItem.RecycleBin))
                };
                RefreshItemViews();
                if (!SaveSettings()) PauseMoves();
            }, () => !transferRecoveryBlocked && !moveSettingsBlocked));
        }
        menu.Items.Add(Command(IsPortal ? "Open linked folder" : "Open Pen storage folder", () =>
        {
            ItemStatus.Text = IsPortal
                ? ShellItemLauncher.Open(settings.PortalFolder!) ?? string.Empty
                : moves is null
                    ? "Windows could not locate the user desktop."
                    : ShellItemLauncher.Open(moves.StorageDirectory) ?? string.Empty;
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(Command("Hide Pen", HidePen));
        var delete = Command(IsPortal ? "Delete Folder Portal…" : "Delete Pen…",
            () => DeleteRequested?.Invoke(this), () => IsPortal || settings.ItemPaths.Length == 0);
        delete.ToolTip = "Removes the container. Empty an ordinary Pen before deleting it.";
        menu.Items.Add(delete);
        return menu;
    }
}
