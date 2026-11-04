using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

/// <summary>Transient UI only. No workspace or storage is created until the explicit menu action.</summary>
internal sealed class CreationPreview : Window
{
    private readonly ContextMenu menu;
    private bool closed;

    internal MenuItem CreateItem { get; }
    internal MenuItem PortalItem { get; }

    public CreationPreview(Rect region, Action<Rect> create)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Width = region.Width;
        Height = region.Height;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(90, 20, 20, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 180, 200, 235)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6)
        };
        CreateItem = new MenuItem { Header = "Create a Pen here" };
        PortalItem = new MenuItem { Header = "Create a Folder Portal here", IsEnabled = false, ToolTip = "Coming later" };
        ToolTipService.SetShowOnDisabled(PortalItem, true);
        menu = new ContextMenu { PlacementTarget = this, Placement = PlacementMode.RelativePoint };
        menu.Items.Add(CreateItem);
        menu.Items.Add(PortalItem);
        CreateItem.Click += (_, _) =>
        {
            Close();
            create(region);
        };
        menu.Closed += (_, _) => { if (!closed) Close(); };
        Loaded += (_, _) =>
        {
            DesktopGeometry.Place(this, region);
            menu.HorizontalOffset = ActualWidth;
            menu.VerticalOffset = Math.Max(0, ActualHeight - 24);
            Activate(); // Explicit user-requested creation menu may take keyboard focus.
            menu.IsOpen = true;
        };
        Closing += (_, _) => { closed = true; menu.IsOpen = false; };
    }
}
