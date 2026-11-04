using System.IO;
using System.Windows;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenItemTests
{
    [Fact]
    public void MembershipAddsUniqueExistingReferencesAndNeverChangesSources()
    {
        WithDirectory(directory =>
        {
            var file = Path.Combine(directory, "Notes.txt");
            File.WriteAllText(file, "Original content");
            var shortcut = Path.Combine(directory, "Tool.lnk");
            File.WriteAllText(shortcut, "Shortcut fixture");
            var members = PenMembership.Add([], [file, file.ToUpperInvariant(), shortcut,
                Path.Combine(directory, "Missing.txt"), "relative.txt"]);
            Assert.Equal(new[] { file, shortcut }, members);
            var remaining = PenMembership.Remove(members, file.ToUpperInvariant());
            Assert.Equal(new[] { shortcut }, remaining);
            Assert.Equal("Original content", File.ReadAllText(file));
            Assert.Equal("Shortcut fixture", File.ReadAllText(shortcut));
        });
    }

    [Fact]
    public void NormalizeKeepsMissingReferencesAndRejectsMalformedPaths()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.txt");
        Assert.Equal(new[] { missing }, PenMembership.Normalize([missing, missing.ToUpperInvariant(),
            "relative.txt", string.Empty, "C:\\bad\0path"]));
    }

    [Theory]
    [InlineData("Programs.lnk", "Programs")]
    [InlineData("Website.url", "Website")]
    [InlineData("Notes.txt", "Notes.txt")]
    [InlineData("Book.epub", "Book.epub")]
    public void DisplayNamesKeepDocumentExtensions(string name, string expected)
    {
        Assert.Equal(expected, new PenItem(Path.Combine(Path.GetTempPath(), name)).DisplayName);
    }

    [Theory]
    [InlineData(DragDropEffects.All, true, DragDropEffects.Copy)]
    [InlineData(DragDropEffects.Link, true, DragDropEffects.Link)]
    [InlineData(DragDropEffects.Move, true, DragDropEffects.None)]
    [InlineData(DragDropEffects.Copy, false, DragDropEffects.None)]
    public void IncomingDropsNeverOfferMove(DragDropEffects allowed, bool hasFiles, DragDropEffects expected)
    {
        Assert.Equal(expected, ItemDragPolicy.IncomingEffect(allowed, hasFiles));
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void OnlyAnUncancelledDesktopReleaseRemovesMembership(bool escaped, bool leftDown, bool desktop, bool expected)
    {
        Assert.Equal(expected, ItemDragPolicy.ShouldRemove(escaped, leftDown, desktop));
    }

    [Theory]
    [InlineData(42, 42, "Progman", true)]
    [InlineData(42, 42, "WorkerW", true)]
    [InlineData(42, 42, "CabinetWClass", false)]
    [InlineData(42, 42, "Shell_TrayWnd", false)]
    [InlineData(42, 99, "WorkerW", false)]
    [InlineData(0, 0, "Progman", false)]
    public void DesktopDetectionExcludesExplorerFoldersAndOtherProcesses(uint shell, uint target, string root, bool expected)
    {
        Assert.Equal(expected, DesktopDropTarget.IsDesktop(shell, target, root));
    }

    [Fact]
    public void MembershipSurvivesRestartAndOlderSettingsRemainCompatible()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "pen.json");
            File.WriteAllText(path, "{\"Title\":\"Existing Pen\",\"IsVisible\":false}");
            var store = new PenSettingsStore(path);
            var legacy = store.Load();
            Assert.Empty(legacy.ItemPaths);
            Assert.Equal("Existing Pen", legacy.Title);
            Assert.False(legacy.IsVisible);
            var missing = Path.Combine(directory, "Unavailable.lnk");
            Assert.True(store.Save(legacy with { ItemPaths = [missing] }));
            var restored = new PenSettingsStore(path).Load();
            Assert.Equal(new[] { missing }, restored.ItemPaths);
            Assert.Equal("Existing Pen", restored.Title);
            Assert.False(restored.IsVisible);
        });
    }

    [Fact]
    public void MissingItemOpenReportsAnErrorWithoutRemovingTheReference()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.txt");
        Assert.NotNull(ShellItemLauncher.Open(path));
        Assert.Equal(new[] { path }, PenMembership.Normalize([path]));
    }

    private static void WithDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PennCraft-items", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
