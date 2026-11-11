using System.Text.Json;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class LayoutTransactionTests
{
    [Fact]
    public void SuccessfulLayoutAndUndoChangeOnlyGeometry()
    {
        WithWorkspace((workspace, store, service) =>
        {
            var original = store.Load();
            var next = original with { Left = 700, Top = 24, Width = 600, Height = 420 };
            Assert.True(service.Execute(new() { ["Default"] = original }, () => new() { ["Default"] = next }, () => Assert.Fail("No rollback")));
            Assert.Equivalent(next, store.Load(), strict: true);
            var edited = store.Load() with { Title = "Edited after layout", IsVisible = false };
            Assert.True(store.Save(edited));
            Assert.True(service.Execute(new() { ["Default"] = edited }, () => new() { ["Default"] = original }, () => Assert.Fail("No rollback")));
            Assert.Equal(original.Left, store.Load().Left);
            Assert.Equal("Edited after layout", store.Load().Title);
            Assert.False(store.Load().IsVisible);
            Assert.Equal(original.ItemPaths, store.Load().ItemPaths);
        });
    }

    [Fact]
    public void FailedApplyRollsBackUiAndSavedGeometry()
    {
        WithWorkspace((_, store, service) =>
        {
            var original = store.Load();
            var rolledBack = false;
            Assert.False(service.Execute(new() { ["Default"] = original }, () => throw new InvalidOperationException(),
                () => rolledBack = true));
            Assert.True(rolledBack);
            Assert.Equivalent(original, store.Load(), strict: true);
            Assert.Null(service.BlockedReason);
        });
    }

    [Fact]
    public void InterruptedCommitRecoversBeforeOpeningWithoutRevertingMetadataEdits()
    {
        WithWorkspace((workspace, store, service) =>
        {
            var original = store.Load();
            File.WriteAllText(service.JournalPath, JsonSerializer.Serialize(new LayoutJournal(1, new()
            {
                ["Default"] = new(original.Left, original.Top, original.Width, original.Height)
            })));
            Assert.True(store.Save(original with { Left = 900, Title = "Keep new title" }));
            Assert.Null(new LayoutTransaction(workspace).Recover());
            Assert.Equal(original.Left, store.Load().Left);
            Assert.Equal("Keep new title", store.Load().Title);
            Assert.Null(new LayoutTransaction(workspace).Recover());
        });
    }

    [Fact]
    public void LockedStorePreflightDoesNotInvokeWindowPlacement()
    {
        WithWorkspace((workspace, store, service) =>
        {
            using var locked = new FileStream(workspace.SettingsPath("Default"), FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.False(service.Execute(new() { ["Default"] = store.Load() },
                () => { Assert.Fail("Must not apply"); return []; }, () => Assert.Fail("Must not roll back untouched UI")));
            Assert.False(File.Exists(service.JournalPath));
        });
    }

    [Fact]
    public void PartialCommitKeepsARecoveryRecordAndRestoresEveryParticipantsGeometry()
    {
        WithWorkspace((workspace, store, service) =>
        {
            var otherId = Guid.NewGuid().ToString("N");
            workspace.Save(workspace.Load() with { PenIds = ["Default", otherId] });
            var otherStore = new PenSettingsStore(workspace.SettingsPath(otherId));
            var original = store.Load();
            var other = new PenSettings { Left = 20, Top = 40, Title = "Other" };
            Assert.True(otherStore.Save(other));
            FileStream? locked = null;
            try
            {
                Assert.False(service.Execute(new() { ["Default"] = original, [otherId] = other }, () =>
                {
                    locked = new FileStream(workspace.SettingsPath(otherId), FileMode.Open, FileAccess.Read, FileShare.Read);
                    return new() { ["Default"] = original with { Left = 700 }, [otherId] = other with { Left = 1300 } };
                }, () => { }));
            }
            finally { locked?.Dispose(); }
            Assert.NotNull(service.BlockedReason);
            Assert.True(File.Exists(service.JournalPath));
            Assert.Null(new LayoutTransaction(workspace).Recover());
            Assert.Equal(original.Left, store.Load().Left);
            Assert.Equal(other.Left, otherStore.Load().Left);
            Assert.Equal(other.Title, otherStore.Load().Title);
        });
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Before\":{}}")]
    public void CorruptOrIncompleteRecoveryRecordsAreRetained(string text)
    {
        WithWorkspace((_, store, service) =>
        {
            var original = store.Load();
            File.WriteAllText(service.JournalPath, text);
            Assert.NotNull(service.Recover());
            Assert.Equal(text, File.ReadAllText(service.JournalPath));
            Assert.Equivalent(original, store.Load(), strict: true);
        });
    }

    private static void WithWorkspace(Action<WorkspaceStore, PenSettingsStore, LayoutTransaction> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-layout", Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new WorkspaceStore(root);
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            var store = new PenSettingsStore(workspace.SettingsPath("Default"));
            Assert.True(store.Save(new PenSettings
            {
                Title = "Keep title",
                Left = -100,
                Width = 400,
                Height = 250,
                ItemPaths = [Path.Combine(root, "Reference.txt")]
            }));
            action(workspace, store, new LayoutTransaction(workspace));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
