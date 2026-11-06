using System.Windows;
using System.Windows.Controls;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

internal sealed class AboutDialog : Window
{
    internal AboutDialog()
    {
        Title = "About PennCraft";
        Icon = AppInfo.CreateWindowIcon();
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new Image { Source = Icon, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Left });
        panel.Children.Add(new TextBlock { Text = "PennCraft", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
        panel.Children.Add(new TextBlock { Text = $"Version {AppInfo.Version} · Windows x64 beta" });
        panel.Children.Add(new TextBlock
        {
            Text = "Make room for everything.\n\nThis unsigned beta is still being tested. Keep a backup before organizing important files.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12)
        });
        void Link(string label, string target)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(8, 5, 8, 5) };
            button.Click += (_, _) =>
            {
                var error = target.StartsWith("https://", StringComparison.Ordinal)
                    ? AppInfo.OpenWebsite(target) : ShellItemLauncher.Open(target);
                if (error is not null) MessageBox.Show(this, error, "PennCraft");
            };
            panel.Children.Add(button);
        }
        Link("Downloads and release notes", AppInfo.RepositoryUrl + "/releases");
        Link("Report a problem", AppInfo.RepositoryUrl + "/issues/new/choose");
        Link("Open settings and logs", AppInfo.DataDirectory);
        panel.Children.Add(new Button { Content = "Close", IsCancel = true, IsDefault = true, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8, 5, 8, 5) });
        Content = panel;
    }
}
