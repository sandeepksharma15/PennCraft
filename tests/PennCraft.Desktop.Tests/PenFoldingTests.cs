using System.IO;
using System.Windows;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

[Collection("Pen close UI")]
public sealed class PenFoldingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoldedStatePreservesExpandedGeometryAcrossMovesLayoutHideAndRestart(bool portal)
    {
        await PenWindowTestHost.RunAsync(portal, async (window, store, root) =>
        {
            await window.LayoutReady;
            var original = window.TransferSnapshot();
            Assert.True(window.ToggleFold());
            Assert.True(window.IsFolded);
            Assert.Equal(MainWindow.FoldedHeight, window.Height);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("PenContent")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("ResizeGrips")).Visibility);
            window.Left += 24;
            window.Top += 12;
            window.HidePen();
            var folded = store.Load();
            Assert.True(folded.IsFolded);
            Assert.False(folded.IsVisible);
            Assert.Equal(original.Height, folded.Height);
            Assert.Equal(original.Width, folded.Width);
            Assert.Equal(original.Left + 24, folded.Left);
            window.ShowPen();
            var screen = DesktopGeometry.PrimaryLayoutScreen();
            var width = PenLayoutMetrics.CommonWidth(SystemParameters.VerticalScrollBarWidth);
            var plan = PenLayoutPlanner.Plan(screen.WorkArea, screen.Scale, width,
                [window.MeasureLayoutItem(width)], screen.GridOrigin);
            Assert.Null(plan.Error);
            window.BeginLayout();
            try { window.ApplyLayoutBounds(plan.Bounds[window.PenId]); }
            finally { window.EndLayout(); }
            Assert.Equal(plan.Bounds[window.PenId], DesktopGeometry.Bounds(window));
            window.HidePen();
            Assert.Equal(original.Height, store.Load().Height);
            Assert.True(window.ToggleFold());
            Assert.Equal(original.Height, window.Height);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("PenContent")).Visibility);
            Assert.True(window.ToggleFold());
            window.ExitApplication();

            var restored = new MainWindow(store, Guid.NewGuid().ToString("N"), root);
            try
            {
                await restored.LayoutReady;
                Assert.True(restored.IsFolded);
                Assert.Equal(MainWindow.FoldedHeight, restored.Height);
                Assert.True(restored.ToggleFold());
                Assert.Equal(original.Height, restored.Height);
                Assert.Equal(original.PortalFolder, store.Load().PortalFolder);
                Assert.Equal(original.ItemPaths, store.Load().ItemPaths);
            }
            finally { restored.ExitApplication(); }
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "Item.txt")));
        });
    }

    [Fact]
    public async Task BusyOperationsAndFailedWritesLeaveFoldStateUnchanged()
    {
        await PenWindowTestHost.RunAsync(false, async (window, store, root) =>
        {
            await window.ItemsReady;
            window.ReserveTransfer();
            Assert.False(window.ToggleFold());
            window.ReleaseTransfer();
            var before = store.Load();
            using (var locked = new FileStream(Path.Combine(root, "pen.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.False(window.ToggleFold());
            Assert.False(window.IsFolded);
            Assert.Equal(before.Height, window.Height);
            Assert.Equivalent(before, store.Load(), strict: true);
        });
    }

    [Fact]
    public void FoldedHeadersUseCompactLayoutSpaceWithoutThreeRowMinimum()
    {
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 200), 1, 600,
            [new("one", new Rect(0, 0, 600, 36), 100, 60, true),
             new("two", new Rect(0, 50, 600, 36), 0, 60, true)]);
        Assert.Null(plan.Error);
        Assert.Equal(new Rect(1296, 24, 600, 36), plan.Bounds["one"]);
        Assert.Equal(new Rect(1296, 84, 600, 36), plan.Bounds["two"]);
    }

    [Fact]
    public void InsufficientSpaceDescribesFoldedHeaders()
    {
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 60), 1, 600,
            [new("folded", new Rect(0, 0, 600, 36), 0, 60, true)]);
        Assert.Empty(plan.Bounds);
        Assert.Contains("folded Pen header", plan.Error);
    }

}
