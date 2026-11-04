using System.IO;
using PennCraft.Desktop.Items;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class WorkspaceStoreTests
{
    [Fact]
    public void MigrationRetainsExistingDefaultSettingsAndStorageIdentity()
    {
        WithDirectory(directory =>
        {
            var legacy = Path.Combine(directory, "pen.json");
            var bytes = "{\"Title\":\"My tools\",\"IsVisible\":false}";
            File.WriteAllText(legacy, bytes);
            var store = new WorkspaceStore(directory);
            var settings = store.Load();
            Assert.Equal(new[] { "Default" }, settings.PenIds);
            Assert.Equal(legacy, store.SettingsPath("Default"));
            Assert.Equal(bytes, File.ReadAllText(legacy));
            Assert.False(new PenSettingsStore(legacy).Load().IsVisible);
            Assert.Equal(settings.PenIds, store.Load().PenIds);
        });
    }

    [Fact]
    public void MultiplePensPersistIndependentTitleGeometryVisibilityAndMembership()
    {
        WithDirectory(directory =>
        {
            var store = new WorkspaceStore(directory);
            var first = Guid.NewGuid().ToString("N");
            var second = Guid.NewGuid().ToString("N");
            var one = new PenSettings { Title = "Tools", Left = -300, Width = 450, IsVisible = false,
                ItemPaths = [Path.Combine(directory, "One.txt")] };
            var two = new PenSettings { Title = "Notes", Top = 250, BackgroundOpacity = 0.55,
                ItemPaths = [Path.Combine(directory, "Two.txt")] };
            Assert.True(new PenSettingsStore(store.SettingsPath(first)).Save(one));
            Assert.True(new PenSettingsStore(store.SettingsPath(second)).Save(two));
            store.Save(new WorkspaceSettings(1, [first, second], false));
            var restored = new WorkspaceStore(directory);
            Assert.Equal(new[] { first, second }, restored.Load().PenIds);
            Assert.False(restored.Load().CreationEnabled);
            Assert.Equivalent(one, new PenSettingsStore(restored.SettingsPath(first)).Load(), strict: true);
            Assert.Equivalent(two, new PenSettingsStore(restored.SettingsPath(second)).Load(), strict: true);
        });
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("Other")]
    [InlineData("")]
    public void InvalidIdsCannotEscapeSettingsOrStorage(string id)
    {
        WithDirectory(directory =>
        {
            var store = new WorkspaceStore(directory);
            Assert.Throws<ArgumentException>(() => store.SettingsPath(id));
            Assert.Throws<ArgumentException>(() => new PenMoveService(directory, id));
        });
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"Version\":2,\"PenIds\":[\"Default\"]}")]
    [InlineData("{\"Version\":1,\"PenIds\":[\"Default\",\"Default\"]}")]
    public void CorruptOrUnsupportedIndexIsLeftUntouched(string text)
    {
        WithDirectory(directory =>
        {
            var file = Path.Combine(directory, "workspace.json");
            File.WriteAllText(file, text);
            Assert.ThrowsAny<Exception>(() => new WorkspaceStore(directory).Load());
            Assert.Equal(text, File.ReadAllText(file));
        });
    }

    [Fact]
    public void SeparatePensKeepSameNamedFilesInTheirOwnStorageAndJournals()
    {
        WithDirectory(desktop =>
        {
            var first = new PenMoveService(desktop);
            var secondId = Guid.NewGuid().ToString("N");
            var second = new PenMoveService(desktop, secondId);
            Assert.Null(first.Recover([]).Error);
            Assert.Null(second.Recover([]).Error);
            var source = Path.Combine(desktop, "Same.txt");
            File.WriteAllText(source, "First");
            var one = first.MoveInto(source);
            Assert.True(one.Moved, one.Error);
            Assert.Null(first.Acknowledge([one.OperationId!.Value]));
            File.WriteAllText(source, "Second");
            var two = second.MoveInto(source);
            Assert.True(two.Moved, two.Error);
            Assert.Null(second.Acknowledge([two.OperationId!.Value]));
            Assert.NotEqual(first.JournalPath, second.JournalPath);
            Assert.Equal("First", File.ReadAllText(one.Destination));
            Assert.Equal("Second", File.ReadAllText(two.Destination));
            Assert.NotNull(first.MoveInto(two.Destination).Error);
            Assert.NotNull(second.MoveInto(one.Destination).Error);
            var pathBeforeRename = second.StorageDirectory;
            Assert.Equal(pathBeforeRename, new PenMoveService(desktop, secondId).StorageDirectory);
        });
    }

    [Fact]
    public async Task ConcurrentPensCannotPrepareTwoMovesForTheSameDesktopFile()
    {
        var desktop = Path.Combine(Path.GetTempPath(), "PennCraft-concurrent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(desktop);
        try
        {
            var first = new PenMoveService(desktop);
            var second = new PenMoveService(desktop, Guid.NewGuid().ToString("N"));
            first.Recover([]);
            second.Recover([]);
            var source = Path.Combine(desktop, "Shared.txt");
            File.WriteAllText(source, "Keep");
            var results = await Task.WhenAll(
                Task.Run(() => first.MoveInto(source), TestContext.Current.CancellationToken),
                Task.Run(() => second.MoveInto(source), TestContext.Current.CancellationToken));
            var completed = Assert.Single(results.Where(result => result.Moved));
            Assert.NotNull(Assert.Single(results.Where(result => !result.Moved)).Error);
            Assert.Equal("Keep", File.ReadAllText(completed.Destination));
            Assert.False(File.Exists(source));
            Assert.Equal(1, MoveJournalFile.Read(first.JournalPath).Length + MoveJournalFile.Read(second.JournalPath).Length);
        }
        finally { Directory.Delete(desktop, recursive: true); }
    }

    private static void WithDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PennCraft-workspace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
