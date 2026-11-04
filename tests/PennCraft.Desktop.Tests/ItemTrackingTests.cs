using System.IO;
using System.Runtime.InteropServices;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class ItemTrackingTests
{
    [Fact]
    public void RenamedItemFollowsItsIdentityAndSurvivesRestart()
    {
        WithDirectory(root =>
        {
            var source = Path.Combine(root, "Old.txt");
            var renamed = Path.Combine(root, "New.txt");
            File.WriteAllText(source, "Keep");
            var tracking = ItemTracking.Capture([source], []);
            var store = new PenSettingsStore(Path.Combine(root, "pen.json"));
            Assert.True(store.Save(new PenSettings { ItemPaths = [source], TrackedItems = tracking }));
            Assert.Null(ItemRename.Rename(source, "New.txt"));
            var saved = store.Load();
            var result = ItemTracking.Reconcile(saved.ItemPaths, saved.TrackedItems);
            Assert.Equal(new[] { renamed }, result.Paths);
            Assert.Equal(tracking[0].Identity, Assert.Single(result.Tracking).Identity);
            Assert.True(store.Save(saved with { ItemPaths = result.Paths, TrackedItems = result.Tracking }));
            Assert.Equal(new[] { renamed }, store.Load().ItemPaths);
            Assert.Equal("Keep", File.ReadAllText(renamed));
        });
    }

    [Fact]
    public void DeletedTrackedItemIsRemovedWithoutAdoptingAnUnrelatedReplacement()
    {
        WithDirectory(root =>
        {
            var items = Path.Combine(root, "Items");
            var outside = Path.Combine(root, "Archive");
            Directory.CreateDirectory(items);
            Directory.CreateDirectory(outside);
            var source = Path.Combine(items, "Same.txt");
            File.WriteAllText(source, "Original");
            var tracked = ItemTracking.Capture([source], []);
            File.Move(source, Path.Combine(outside, "Original.txt"));
            File.WriteAllText(source, "Replacement");
            var result = ItemTracking.Reconcile([source], tracked);
            Assert.Empty(result.Paths);
            Assert.Equal("Replacement", File.ReadAllText(source));
            Assert.Equal("Original", File.ReadAllText(Path.Combine(outside, "Original.txt")));
        });
    }

    [Fact]
    public void DeletionRemovesOnlyTheDeletedMembership()
    {
        WithDirectory(root =>
        {
            var first = Path.Combine(root, "First.txt");
            var second = Path.Combine(root, "Second.txt");
            File.WriteAllText(first, "First");
            File.WriteAllText(second, "Second");
            var tracking = ItemTracking.Capture([first, second], []);
            File.Delete(first); // Simulates a completed shell delete only in this test fixture.
            var result = ItemTracking.Reconcile([first, second], tracking);
            Assert.Equal(new[] { second }, result.Paths);
            Assert.False(result.Uncertain);
            Assert.Equal("Second", File.ReadAllText(second));
        });
    }

    [Fact]
    public void UnavailableParentRetainsTheReference()
    {
        WithDirectory(root =>
        {
            var folder = Path.Combine(root, "Items");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "Keep.txt");
            File.WriteAllText(path, "Keep");
            var tracking = ItemTracking.Capture([path], []);
            Directory.Move(folder, Path.Combine(root, "Offline"));
            var result = ItemTracking.Reconcile([path], tracking);
            Assert.True(result.Uncertain);
            Assert.Equal(new[] { path }, result.Paths);
            Assert.Equal(tracking, result.Tracking);
        });
    }

    [Fact]
    public void UnknownLegacyMissingItemsAreNotSilentlyRemoved()
    {
        WithDirectory(root =>
        {
            var path = Path.Combine(root, "Missing.txt");
            Assert.Empty(ItemTracking.Capture([path], []));
            Assert.Equal(new[] { path }, ItemTracking.Reconcile([path], []).Paths);
        });
    }

    [Fact]
    public void AmbiguousHardLinksDoNotGuessARename()
    {
        WithDirectory(root =>
        {
            var old = Path.Combine(root, "Original.txt");
            var first = Path.Combine(root, "First.txt");
            var second = Path.Combine(root, "Second.txt");
            File.WriteAllText(old, "Keep");
            var tracking = ItemTracking.Capture([old], []);
            File.Move(old, first);
            Assert.True(CreateHardLink(second, first, 0));
            var result = ItemTracking.Reconcile([old], tracking);
            Assert.True(result.Uncertain);
            Assert.Equal(new[] { old }, result.Paths);
            Assert.Equal("Keep", File.ReadAllText(first));
            Assert.Equal("Keep", File.ReadAllText(second));
        });
    }

    [Fact]
    public void InvalidTrackingMetadataCannotRemoveMembership()
    {
        WithDirectory(root =>
        {
            var path = Path.Combine(root, "Keep.txt");
            var settings = new PenSettings { ItemPaths = [path], TrackedItems = [new(path, "invalid")] }.Normalize();
            Assert.Empty(settings.TrackedItems);
            Assert.Equal(new[] { path }, ItemTracking.Reconcile(settings.ItemPaths, settings.TrackedItems).Paths);
        });
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("CON.txt")]
    [InlineData("LPT1")]
    [InlineData("bad:name.txt")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("")]
    public void InvalidRenameCannotEscapeTheOriginalFolder(string name) =>
        Assert.NotNull(ItemRename.ValidateName(name));

    [Fact]
    public void RenameConflictNeverOverwritesEitherItem()
    {
        WithDirectory(root =>
        {
            var source = Path.Combine(root, "First.txt");
            var existing = Path.Combine(root, "Second.txt");
            File.WriteAllText(source, "First");
            File.WriteAllText(existing, "Second");
            Assert.NotNull(ItemRename.Rename(source, "Second.txt"));
            Assert.Equal("First", File.ReadAllText(source));
            Assert.Equal("Second", File.ReadAllText(existing));
        });
    }

    [Fact]
    public void RenameShortcutPreservesItsBytesAndUnrelatedTarget()
    {
        WithDirectory(root =>
        {
            var link = Path.Combine(root, "Shortcut.lnk");
            var target = Path.Combine(root, "Target.txt");
            File.WriteAllBytes(link, [1, 9, 22, 255]);
            File.WriteAllText(target, "Target");
            var identity = NativeFileMove.Identity(link);
            Assert.Null(ItemRename.Rename(link, "Renamed.lnk"));
            var renamed = Path.Combine(root, "Renamed.lnk");
            Assert.Equal(identity, NativeFileMove.Identity(renamed));
            Assert.Equal(new byte[] { 1, 9, 22, 255 }, File.ReadAllBytes(renamed));
            Assert.Equal("Target", File.ReadAllText(target));
        });
    }

    [Fact]
    public void RenameRefusesAReplacementCreatedWhileTheMenuWasOpen()
    {
        WithDirectory(root =>
        {
            var source = Path.Combine(root, "Original.txt");
            File.WriteAllText(source, "Original");
            var identity = NativeFileMove.Identity(source);
            File.Move(source, Path.Combine(root, "Preserved.txt"));
            File.WriteAllText(source, "Replacement");
            Assert.NotNull(ItemRename.Rename(source, "Renamed.txt", identity));
            Assert.Equal("Replacement", File.ReadAllText(source));
            Assert.Equal("Original", File.ReadAllText(Path.Combine(root, "Preserved.txt")));
            Assert.False(File.Exists(Path.Combine(root, "Renamed.txt")));
        });
    }

    private static void WithDirectory(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-context-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateHardLinkW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, nint security);
}
