using System.IO;
using System.Windows;
using System.Windows.Controls;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

internal sealed class RenameItemDialog : Window
{
    private readonly TextBox name;
    public string ItemName => name.Text;

    public RenameItemDialog(Window owner, string path)
    {
        Owner = owner;
        Title = "Rename item";
        Width = 420;
        Height = 180;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "New name", Margin = new Thickness(0, 0, 0, 6) });
        name = new TextBox { Text = Path.GetFileName(path) };
        panel.Children.Add(name);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var rename = new Button { Content = "Rename", IsDefault = true, MinWidth = 80, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(4) };
        rename.Click += (_, _) =>
        {
            error.Text = ItemRename.ValidateName(name.Text) ?? string.Empty;
            if (error.Text.Length == 0) DialogResult = true;
        };
        buttons.Children.Add(rename);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            name.Focus();
            var dot = name.Text.LastIndexOf('.');
            name.Select(0, dot > 0 ? dot : name.Text.Length);
        };
    }
}
