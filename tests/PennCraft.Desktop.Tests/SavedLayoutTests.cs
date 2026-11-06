using System.IO;
using System.Text.Json;
using System.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class SavedLayoutTests
{
    private static LayoutPresentation Presentation(PenSettings? settings = null) =>
        LayoutPresentation.Capture(settings ?? new PenSettings(), new Rect(100, 100, 600, 300));

    [Fact]
    public void CatalogRoundTripsRenamesDeletesAndRejectsDuplicateNames()
    {
        WithDirectory(root =>
        {
            var path = Path.Combine(root, "layouts.json");
            var store = new SavedLayoutStore(path);
            Assert.Empty(store.Load());
            var created = store.Add("  Daily work  ", "display", new() { ["Default"] = Presentation() });
            Assert.Equal("Daily work", Assert.Single(new SavedLayoutStore(path).Load()).Name);
            Assert.Throws<InvalidDataException>(() => store.Add("DAILY WORK", "display", created.Pens));
            store.Rename(created.Id, "Development");
            Assert.Equal("Development", Assert.Single(store.Load()).Name);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => store.Rename(created.Id, "Unsaved"));
            Assert.Equal("Development", Assert.Single(store.Load()).Name);
            store.Delete(created.Id);
            Assert.Empty(new SavedLayoutStore(path).Load());
        });
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("{\"Version\":9,\"Layouts\":[]}")]
    [InlineData("{\"Version\":1,\"Layouts\":[null]}")]
    public void CorruptCatalogIsRetainedAndCannotBeOverwritten(string text)
    {
        WithDirectory(root =>
        {
            var path = Path.Combine(root, "layouts.json");
            File.WriteAllText(path, text);
            var failure = Record.Exception(() => new SavedLayoutStore(path).Add("New", "display", new() { ["Default"] = Presentation() }));
            Assert.True(failure is JsonException or InvalidDataException);
            Assert.Equal(text, File.ReadAllText(path));
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nname")]
    public void NamesMustBeNonemptyAndReadable(string name) => Assert.NotNull(SavedLayoutStore.NameError(name));

    [Fact]
    public void PlannerSkipsDeletedContainersLeavesNewOnesAloneAndClampsOffscreenBounds()
    {
        var removed = Guid.NewGuid().ToString("N");
        var added = Guid.NewGuid().ToString("N");
        var original = Presentation() with { Bounds = new(-1500, -100, 600, 300) };
        var layout = new SavedLayout(Guid.NewGuid().ToString("N"), "Work", "display",
            new() { ["Default"] = original, [removed] = Presentation() });
        var plan = SavedLayoutPlanner.Plan(layout, "display", ["Default", added],
            [new Rect(-1920, 0, 1920, 1080), new Rect(0, 0, 1920, 1080)]);
        Assert.Null(plan.Error);
        Assert.Equal(1, plan.MissingCount);
        Assert.Single(plan.Pens);
        Assert.Equal(new Rect(-1500, 0, 600, 300), plan.Pens["Default"].Bounds.ToRect());
        Assert.Equal(-100, original.Bounds.Y);
        Assert.NotNull(SavedLayoutPlanner.Plan(layout, "changed", ["Default"], [new Rect(0, 0, 1920, 1080)]).Error);
        Assert.Empty(SavedLayoutPlanner.Plan(layout, "display", ["Default"], [new Rect(0, 0, 400, 200)]).Pens);
        Assert.Empty(SavedLayoutPlanner.Plan(layout, "display", [], [new Rect(0, 0, 1920, 1080)]).Pens);
    }

    [Fact]
    public void PresentationMergeKeepsCurrentMetadataAndRejectsInvalidData()
    {
        var current = new PenSettings
        {
            Title = "Current title", PortalFolder = @"D:\Current", ItemPaths = [@"C:\Current.txt"],
            BackgroundOpacity = 0.55
        };
        var saved = Presentation() with { IsVisible = false, IsFolded = true, ItemScale = 1.5 };
        var merged = saved.Merge(current);
        Assert.Equal(current.Title, merged.Title);
        Assert.Equal(current.PortalFolder, merged.PortalFolder);
        Assert.Equal(current.ItemPaths, merged.ItemPaths);
        Assert.Equal(current.BackgroundOpacity, merged.BackgroundOpacity);
        Assert.False(merged.IsVisible);
        Assert.True(merged.IsFolded);
        Assert.Equal(1.5, merged.ItemScale);
        Assert.Throws<InvalidDataException>(() => (saved with { ItemScale = double.NaN }).Validate());
        Assert.Throws<InvalidDataException>(() => (saved with { Height = 10 }).Validate());
    }

    [Fact]
    public void RestoreAndUndoPersistPresentationWithoutRevertingMetadata()
    {
        WithWorkspace((workspace, store, transaction) =>
        {
            var original = Presentation(store.Load());
            var next = original with { Left = 700, IsVisible = false, IsFolded = true, ItemScale = 1.5 };
            Assert.True(transaction.Execute(new() { ["Default"] = original }, () => new() { ["Default"] = next }, () => Assert.Fail("No rollback")));
            var edited = store.Load() with { Title = "Renamed", BackgroundOpacity = 0.35, ItemPaths = [@"C:\New reference.txt"] };
            Assert.True(store.Save(edited));
            Assert.True(transaction.Execute(new() { ["Default"] = next }, () => new() { ["Default"] = original }, () => Assert.Fail("No rollback")));
            Assert.Equal(original.Left, store.Load().Left);
            Assert.Equal(original.IsVisible, store.Load().IsVisible);
            Assert.Equal(original.IsFolded, store.Load().IsFolded);
            Assert.Equal(original.ItemScale, store.Load().ItemScale);
            Assert.Equal(edited.Title, store.Load().Title);
            Assert.Equal(edited.BackgroundOpacity, store.Load().BackgroundOpacity);
            Assert.Equal(edited.ItemPaths, store.Load().ItemPaths);

            using var locked = new FileStream(workspace.SettingsPath("Default"), FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.False(transaction.Execute(new() { ["Default"] = original },
                () => { Assert.Fail("Preflight must stop"); return new(); }, () => Assert.Fail("No window changed")));
            Assert.Null(transaction.BlockedReason); // A cleared journal is not pending recovery.
        });
    }

    [Fact]
    public void FailedApplicationRollsBackAndInterruptedCommitRecoversPresentationOnly()
    {
        WithWorkspace((workspace, store, transaction) =>
        {
            var original = Presentation(store.Load());
            var rolledBack = false;
            Assert.False(transaction.Execute(new() { ["Default"] = original }, () => throw new InvalidOperationException(),
                () => rolledBack = true));
            Assert.True(rolledBack);
            Assert.Null(transaction.BlockedReason);
            File.WriteAllText(transaction.JournalPath, JsonSerializer.Serialize(new PresentationJournal(1, new() { ["Default"] = original })));
            var edited = store.Load() with { Title = "Keep edit", IsVisible = false, IsFolded = true, ItemScale = 1.5, Left = 800 };
            Assert.True(store.Save(edited));
            Assert.Null(new SavedLayoutTransaction(workspace).Recover());
            Assert.Equal(original.Left, store.Load().Left);
            Assert.True(store.Load().IsVisible);
            Assert.False(store.Load().IsFolded);
            Assert.Equal(original.ItemScale, store.Load().ItemScale);
            Assert.Equal(edited.Title, store.Load().Title);
            Assert.Null(new SavedLayoutTransaction(workspace).Recover());
        });
    }

    [Fact]
    public void PartialWriteRetainsJournalUntilAllParticipantsRecover()
    {
        WithWorkspace((workspace, store, transaction) =>
        {
            var id = Guid.NewGuid().ToString("N");
            workspace.Save(new WorkspaceSettings(1, ["Default", id]));
            var second = new PenSettingsStore(workspace.SettingsPath(id));
            Assert.True(second.Save(new PenSettings { Title = "Portal", PortalFolder = @"D:\Current" }));
            var before = new Dictionary<string, LayoutPresentation>
            {
                ["Default"] = Presentation(store.Load()), [id] = Presentation(second.Load())
            };
            FileStream? locked = null;
            try
            {
                Assert.False(transaction.Execute(before, () =>
                {
                    locked = new FileStream(workspace.SettingsPath(id), FileMode.Open, FileAccess.Read, FileShare.Read);
                    return before.ToDictionary(pair => pair.Key, pair => pair.Value with { IsVisible = false, ItemScale = 1.5 });
                }, () => { }));
            }
            finally { locked?.Dispose(); }
            Assert.NotNull(transaction.BlockedReason);
            Assert.True(File.Exists(transaction.JournalPath));
            Assert.Null(new SavedLayoutTransaction(workspace).Recover());
            Assert.True(store.Load().IsVisible);
            Assert.True(second.Load().IsVisible);
            Assert.Equal(1, store.Load().ItemScale);
            Assert.Equal(@"D:\Current", second.Load().PortalFolder);
        });
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Before\":{}}")]
    public void InvalidRecoveryRecordsAreRetained(string text)
    {
        WithWorkspace((_, store, transaction) =>
        {
            var before = store.Load();
            File.WriteAllText(transaction.JournalPath, text);
            Assert.NotNull(transaction.Recover());
            Assert.Equal(text, File.ReadAllText(transaction.JournalPath));
            Assert.Equivalent(before, store.Load(), strict: true);
        });
    }

    private static void WithWorkspace(Action<WorkspaceStore, PenSettingsStore, SavedLayoutTransaction> test) =>
        WithDirectory(root =>
        {
            var workspace = new WorkspaceStore(root);
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            var store = new PenSettingsStore(workspace.SettingsPath("Default"));
            Assert.True(store.Save(new PenSettings { Title = "Keep title", Width = 420, Height = 260 }));
            test(workspace, store, new SavedLayoutTransaction(workspace));
        });

    private static void WithDirectory(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-snapshots", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
