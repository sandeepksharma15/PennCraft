using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

[Collection("Pen close UI")]
public sealed class PenViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScalingPersistsWithoutChangingWindowOrMembershipAndLayoutFitsScaledItems(bool portal)
    {
        await PenWindowTestHost.RunAsync(portal, async (window, store, root) =>
        {
            await window.LayoutReady;
            var original = window.TransferSnapshot();
            var before = new Rect(window.Left, window.Top, window.Width, window.Height);
            foreach (var value in new[] { 0.75, 1.0, 1.25, 1.5 })
            {
                Assert.True(window.SetItemScale(value));
                Assert.Equal(value, store.Load().ItemScale);
                Assert.Equal(before, new Rect(window.Left, window.Top, window.Width, window.Height));
                Assert.Equal(original.ItemPaths, store.Load().ItemPaths);
                Assert.Equal(original.PortalFolder, store.Load().PortalFolder);
            }
            var grid = (ItemsControl)window.FindName("ItemGrid");
            Assert.Equal(1.5, ((ScaleTransform)grid.LayoutTransform).ScaleX);
            Assert.True(window.ToggleFold());
            Assert.True(window.SetItemScale(1.25));
            Assert.True(window.IsFolded);
            Assert.Equal(MainWindow.FoldedHeight, window.Height);
            Assert.True(window.ToggleFold());
            Assert.True(window.SetItemScale(1.5));

            var screen = DesktopGeometry.PrimaryLayoutScreen();
            var width = PenLayoutMetrics.CommonWidth(SystemParameters.VerticalScrollBarWidth, window.ItemScale);
            var plan = PenLayoutPlanner.Plan(screen.WorkArea, screen.Scale, width,
                [window.MeasureLayoutItem(width)], screen.GridOrigin);
            Assert.Null(plan.Error);
            window.BeginLayout();
            try { window.ApplyLayoutBounds(plan.Bounds[window.PenId]); }
            finally { window.EndLayout(); }
            window.UpdateLayout();
            var scroll = (ScrollViewer)window.FindName("ItemScrollViewer");
            Assert.True(scroll.ViewportHeight >= PenLayoutMetrics.MinimumRows * PenLayoutMetrics.CellHeight * 1.5);
            Assert.True(scroll.ViewportWidth >= PenLayoutMetrics.Columns * PenLayoutMetrics.CellWidth * 1.5);
            Point At(int index) => ((FrameworkElement)grid.ItemContainerGenerator.ContainerFromIndex(index))
                .TransformToAncestor(grid).Transform(new Point());
            Assert.Equal(At(0).Y, At(5).Y);
            Assert.True(At(6).Y > At(5).Y);
            Assert.Equal(At(12).Y, At(17).Y);
            window.HidePen();
            window.ExitApplication();

            var restored = new MainWindow(store, Guid.NewGuid().ToString("N"));
            try
            {
                await restored.LayoutReady;
                Assert.Equal(1.5, restored.ItemScale);
                Assert.Equal(1.5, ((ScaleTransform)((ItemsControl)restored.FindName("ItemGrid")).LayoutTransform).ScaleX);
                Assert.False(restored.UserVisible);
                Assert.Equal(original.ItemPaths, restored.TransferSnapshot().ItemPaths);
            }
            finally { restored.ExitApplication(); }
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "Item.txt")));
        }, itemCount: 18);
    }

    [Fact]
    public async Task MenusShowCurrentChoicesRecheckBusyStateAndRollBackFailedWrites()
    {
        await PenWindowTestHost.RunAsync(false, async (window, store, root) =>
        {
            await window.ItemsReady;
            static MenuItem Find(ItemsControl parent, string title) =>
                parent.Items.OfType<MenuItem>().Single(item => (string)item.Header == title);
            var menu = window.BuildPenMenu();
            var sizes = Find(Find(menu, "View"), "Item size");
            Assert.Equal(4, sizes.Items.Count);
            Assert.True(Find(sizes, "100% (default)").IsChecked);
            var large = sizes.Items.OfType<MenuItem>().Last();
            window.ReserveTransfer();
            large.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); // Menu was created before the busy operation.
            Assert.Equal(1, window.ItemScale);
            Assert.False(window.SetItemScale(1.5));
            window.ReleaseTransfer();
            large.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(1.5, window.ItemScale);
            Assert.Single(Find(Find(window.BuildPenMenu(), "View"), "Item size").Items.OfType<MenuItem>(), item => item.IsChecked);
            Assert.False(Find(Find(window.BuildPenMenu(), "Layout"), "Undo last automatic layout").IsEnabled);
            Assert.False(Find(window.BuildPenMenu(), "Delete Pen…").IsEnabled);
            Assert.False(window.SetItemScale(9));
            var previous = store.Load();
            using (var locked = new FileStream(Path.Combine(root, "pen.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False(window.SetItemScale(0.75));
                var opacity = Find(Find(window.BuildPenMenu(), "View"), "Background opacity");
                opacity.Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            Assert.Equivalent(previous, store.Load(), strict: true);
            Assert.Equal(1.5, window.ItemScale);
            Assert.Equal(1.5, ((ScaleTransform)((ItemsControl)window.FindName("ItemGrid")).LayoutTransform).ScaleX);
            Assert.Equal(previous.BackgroundOpacity, window.TransferSnapshot().BackgroundOpacity);
        });
    }
}
