using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

[Collection("Pen close UI")]
public sealed class SavedLayoutWindowTests
{
    [Fact]
    public async Task EmptyCatalogDisablesActionsThatNeedASelection()
    {
        await PenWindowTestHost.RunAsync(false, async (window, _, root) =>
        {
            await window.ItemsReady;
            var dialog = new SavedLayoutsDialog(new SavedLayoutStore(Path.Combine(root, "saved-layouts.json")), window);
            try
            {
                var buttons = ((DockPanel)dialog.Content).Children.OfType<StackPanel>().Single().Children.OfType<Button>();
                Assert.All(buttons.Where(button => (string)button.Content != "Close"), button => Assert.False(button.IsEnabled));
            }
            finally { dialog.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRestoreAndUndoPreserveContentsAndRestoreHiddenFoldedPresentation(bool portal)
    {
        await PenWindowTestHost.RunAsync(portal, async (window, settingsStore, root) =>
        {
            await window.LayoutReady;
            var workspace = new WorkspaceStore(root);
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            var transaction = new SavedLayoutTransaction(workspace);
            var original = window.CapturePresentation();
            var metadata = window.TransferSnapshot();
            var scale = VisualTreeHelper.GetDpi(window).DpiScaleY;
            var target = original with
            {
                Height = original.Height + 30, IsVisible = false, IsFolded = true, ItemScale = 1.5,
                Bounds = new(original.Bounds.X + 24, original.Bounds.Y + 24, original.Bounds.Width, Math.Round(MainWindow.FoldedHeight * scale))
            };
            Assert.Throws<InvalidOperationException>(() => window.ApplyPresentation(target));
            window.BeginLayout();
            try
            {
                Assert.True(transaction.Execute(new() { ["Default"] = original }, () =>
                {
                    window.ApplyPresentation(target);
                    return new() { ["Default"] = window.CapturePresentation() };
                }, () => window.ApplyPresentation(original)));
            }
            finally { window.EndLayout(); }
            Assert.False(window.IsVisible);
            Assert.True(window.IsFolded);
            Assert.Equal(1.5, window.ItemScale);
            Assert.Equal(target.Bounds.ToRect(), DesktopGeometry.Bounds(window));
            Assert.Equal(target.Height, settingsStore.Load().Height);
            Assert.Equal(metadata.ItemPaths, settingsStore.Load().ItemPaths);
            Assert.Equal(metadata.PortalFolder, settingsStore.Load().PortalFolder);
            Assert.Equal(metadata.Title, settingsStore.Load().Title);

            var restarted = new MainWindow(settingsStore, "Default");
            try
            {
                await restarted.LayoutReady;
                Assert.True(restarted.IsFolded);
                Assert.False(restarted.UserVisible);
                Assert.Equal(1.5, restarted.ItemScale);
            }
            finally { restarted.ExitApplication(); }

            var beforeUndo = window.CapturePresentation();
            window.BeginLayout();
            try
            {
                Assert.True(transaction.Execute(new() { ["Default"] = beforeUndo }, () =>
                {
                    window.ApplyPresentation(original);
                    return new() { ["Default"] = window.CapturePresentation() };
                }, () => window.ApplyPresentation(beforeUndo)));
            }
            finally { window.EndLayout(); }
            Assert.False(window.IsFolded);
            Assert.True(window.UserVisible);
            Assert.Equal(original.Bounds.ToRect(), DesktopGeometry.Bounds(window));
            Assert.Equal(original.Height, settingsStore.Load().Height);
            Assert.Equal(original.ItemScale, settingsStore.Load().ItemScale);
            Assert.Equal(metadata.ItemPaths, settingsStore.Load().ItemPaths);
            Assert.Equal(metadata.PortalFolder, settingsStore.Load().PortalFolder);
            Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "Item.txt")));
        }, penId: "Default");
    }
}
