using System.Windows;
using System.Windows.Controls;

namespace PennCraft.Desktop;

/// <summary>Catalog management only; the controller restores after this modal window closes.</summary>
internal sealed class SavedLayoutsDialog : Window
{
    private readonly SavedLayoutStore store;
    private readonly ListBox layouts = new() { DisplayMemberPath = nameof(SavedLayout.Name), MinHeight = 180 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    internal string? SelectedId => (layouts.SelectedItem as SavedLayout)?.Id;

    internal SavedLayoutsDialog(SavedLayoutStore store, Window? owner)
    {
        this.store = store;
        Title = "Saved layouts";
        if (owner is not null) Owner = owner;
        Width = 520;
        Height = 380;
        MinWidth = 420;
        MinHeight = 300;
        ShowInTaskbar = false;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var restore = AddButton(buttons, "Restore", () => { if (SelectedId is not null) DialogResult = true; });
        restore.IsDefault = true;
        var rename = AddButton(buttons, "Rename…", Rename);
        var delete = AddButton(buttons, "Delete…", Delete);
        var close = new Button { Content = "Close", IsCancel = true, MinWidth = 72, Margin = new Thickness(4) };
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        DockPanel.SetDock(status, Dock.Bottom);
        panel.Children.Add(status);
        panel.Children.Add(layouts);
        Content = panel;
        layouts.SelectionChanged += (_, _) =>
        {
            restore.IsEnabled = rename.IsEnabled = delete.IsEnabled = SelectedId is not null;
        };
        restore.IsEnabled = rename.IsEnabled = delete.IsEnabled = false;
        Reload();
    }

    private static Button AddButton(Panel panel, string title, Action action)
    {
        var button = new Button { Content = title, MinWidth = 72, Margin = new Thickness(4) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
        return button;
    }

    private void Reload(string? id = null)
    {
        var entries = store.Load();
        layouts.ItemsSource = entries;
        layouts.SelectedItem = entries.FirstOrDefault(layout => layout.Id == id) ?? entries.FirstOrDefault();
        status.Text = entries.Length == 0 ? "No saved layouts yet. Use Layout → Save current layout, or the tray menu."
            : "Restores container positions, sizes, visibility, fold state and item size. Current contents stay unchanged.";
    }

    private void Rename()
    {
        if (layouts.SelectedItem is not SavedLayout selected) return;
        var dialog = new LayoutNameDialog("Rename saved layout", selected.Name, this);
        if (dialog.ShowDialog() != true) return;
        try { store.Rename(selected.Id, dialog.LayoutName); Reload(selected.Id); }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception)) { status.Text = exception.Message; }
    }

    private void Delete()
    {
        if (layouts.SelectedItem is not SavedLayout selected) return;
        if (MessageBox.Show(this, $"Delete saved layout “{selected.Name}”? Containers and files will stay unchanged.",
            "Delete saved layout", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { store.Delete(selected.Id); Reload(); }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception)) { status.Text = exception.Message; }
    }
}
