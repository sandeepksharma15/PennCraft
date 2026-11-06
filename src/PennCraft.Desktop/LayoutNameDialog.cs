using System.Windows;
using System.Windows.Controls;

namespace PennCraft.Desktop;

internal sealed class LayoutNameDialog : Window
{
    private readonly TextBox name;
    internal string LayoutName => name.Text.Trim();

    internal LayoutNameDialog(string title, string initial, Window? owner)
    {
        Title = title;
        if (owner is not null) Owner = owner;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Layout name", Margin = new Thickness(0, 0, 0, 6) });
        name = new TextBox { Text = initial, MaxLength = 80 };
        panel.Children.Add(name);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 80, Margin = new Thickness(4) };
        save.Click += (_, _) =>
        {
            error.Text = SavedLayoutStore.NameError(name.Text) ?? string.Empty;
            if (error.Text.Length == 0) DialogResult = true;
        };
        buttons.Children.Add(save);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(4) });
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
