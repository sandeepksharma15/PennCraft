using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Items;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenDeletionTests
{
    [Fact]
    public void DeletesOnlyEmptyPenMetadataAndRetainsDesktopItemsAndOtherPens()
    {
        using var fixture = new Fixture();
        var result = fixture.Service.Delete("Default");
        Assert.True(result.Removed, result.Error);
        Assert.Null(result.Error);
        Assert.False(File.Exists(fixture.Workspace.SettingsPath("Default")));
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.Moves.StorageDirectory)));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(fixture.Desktop, "Keep.txt")));
        Assert.True(File.Exists(fixture.Workspace.SettingsPath(fixture.OtherId)));
        Assert.Equal(fixture.OtherId, Assert.Single(fixture.Workspace.Load().PenIds));
        Assert.Null(fixture.Service.Recover());
    }

    [Fact]
    public void NeverUsedPenDeletesWithoutCreatingDesktopStorage()
    {
        using var fixture = new Fixture();
        var service = new PenDeletionService(fixture.Workspace, fixture.Desktop);
        Assert.True(service.Delete(fixture.OtherId).Removed);
        Assert.False(File.Exists(fixture.Workspace.SettingsPath(fixture.OtherId)));
        Assert.False(Directory.Exists(new PenMoveService(fixture.Desktop, fixture.OtherId).StorageDirectory));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("recycle")]
    [InlineData("portal")]
    [InlineData("untracked")]
    [InlineData("metadata")]
    [InlineData("pending")]
    [InlineData("transfer")]
    [InlineData("portal-move")]
    public void RefusesDeletionWhenAnythingNeedsPreserving(string kind)
    {
        using var fixture = new Fixture();
        if (kind == "membership") Assert.True(fixture.Store.Save(new PenSettings { ItemPaths = [Path.Combine(fixture.Desktop, "Missing.txt")] }));
        if (kind == "recycle") Assert.True(fixture.Store.Save(new PenSettings { ItemPaths = [VirtualDesktopItem.RecycleBin] }));
        if (kind == "portal") Assert.True(fixture.Store.Save(new PenSettings { PortalFolder = fixture.Desktop }));
        if (kind == "untracked") File.WriteAllText(Path.Combine(fixture.Moves.StorageDirectory, "Untracked.txt"), "keep");
        if (kind == "metadata") File.WriteAllText(Path.Combine(Path.GetDirectoryName(fixture.Moves.StorageDirectory)!, "Unknown.txt"), "keep");
        if (kind == "pending") MoveJournalFile.Write(fixture.Moves.JournalPath,
            [new MoveEntry(Guid.NewGuid(), Path.Combine(fixture.Desktop, "Missing.txt"), Path.Combine(fixture.Moves.StorageDirectory, "Missing.txt"), "identity", true)]);
        if (kind == "transfer") File.WriteAllText(fixture.Workspace.TransferJournalPath,
            JsonSerializer.Serialize(new { Version = 1, Pending = new { SourcePenId = "Default" } }));
        if (kind == "portal-move") File.WriteAllText(fixture.Workspace.TransferJournalPath + ".portal", "{}");
        var result = fixture.Service.Delete("Default");
        Assert.False(result.Removed);
        Assert.NotNull(result.Error);
        Assert.Contains("Default", fixture.Workspace.Load().PenIds);
        Assert.True(File.Exists(fixture.Workspace.SettingsPath("Default")));
        Assert.True(Directory.Exists(fixture.Moves.StorageDirectory));
        Assert.False(File.Exists(fixture.Service.JournalPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InterruptedDeletionCompletesOnRestartWithoutRecreatingPen(int checkpoint)
    {
        using var fixture = new Fixture();
        var interrupted = new PenDeletionService(fixture.Workspace, fixture.Desktop, point =>
        {
            if ((int)point == checkpoint) throw new IOException("Interrupted");
        });
        var result = interrupted.Delete("Default");
        Assert.False(result.Removed);
        Assert.NotNull(interrupted.BlockedReason);
        Assert.True(File.Exists(interrupted.JournalPath));
        var recovery = new PenDeletionService(fixture.Workspace, fixture.Desktop);
        Assert.Null(recovery.Recover());
        Assert.Null(recovery.Recover());
        Assert.DoesNotContain("Default", fixture.Workspace.Load().PenIds);
        Assert.False(File.Exists(fixture.Workspace.SettingsPath("Default")));
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.Moves.StorageDirectory)));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(fixture.Desktop, "Keep.txt")));
    }

    [Fact]
    public void RecoveryRetainsUnexpectedContentsIntroducedAfterDeletionIntent()
    {
        using var fixture = new Fixture();
        var interrupted = new PenDeletionService(fixture.Workspace, fixture.Desktop, point =>
        {
            if (point == DeletionCheckpoint.IntentSaved) throw new IOException("Interrupted");
        });
        Assert.False(interrupted.Delete("Default").Removed);
        var extra = Path.Combine(fixture.Moves.StorageDirectory, "Later.txt");
        File.WriteAllText(extra, "keep later");
        Assert.NotNull(new PenDeletionService(fixture.Workspace, fixture.Desktop).Recover());
        Assert.Equal("keep later", File.ReadAllText(extra));
        Assert.Contains("Default", fixture.Workspace.Load().PenIds);
        Assert.True(File.Exists(interrupted.JournalPath));
    }

    [Fact]
    public void LockedSettingsRemainRecoverable()
    {
        using var fixture = new Fixture();
        using (var locked = new FileStream(fixture.Workspace.SettingsPath("Default"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = fixture.Service.Delete("Default");
            Assert.False(result.Removed);
            Assert.NotNull(result.Error);
            Assert.Contains("Default", fixture.Workspace.Load().PenIds);
        }
        Assert.Null(new PenDeletionService(fixture.Workspace, fixture.Desktop).Recover());
        Assert.DoesNotContain("Default", fixture.Workspace.Load().PenIds);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"PendingId\":\"../outside\"}")]
    public void CorruptDeletionRecordsNeverDeleteAnything(string contents)
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Service.JournalPath, contents);
        Assert.NotNull(fixture.Service.Recover());
        Assert.Equal(contents, File.ReadAllText(fixture.Service.JournalPath));
        Assert.True(File.Exists(fixture.Workspace.SettingsPath("Default")));
        Assert.True(Directory.Exists(fixture.Moves.StorageDirectory));
    }

    private sealed class Fixture : IDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "PennCraft-delete", Guid.NewGuid().ToString("N"));
        public string Desktop { get; }
        public WorkspaceStore Workspace { get; }
        public string OtherId { get; } = Guid.NewGuid().ToString("N");
        public PenMoveService Moves { get; }
        public PenSettingsStore Store => new(Workspace.SettingsPath("Default"));
        public PenDeletionService Service { get; }

        public Fixture()
        {
            Desktop = Directory.CreateDirectory(Path.Combine(Root, "Desktop")).FullName;
            Workspace = new WorkspaceStore(Path.Combine(Root, "Settings"));
            Workspace.Save(new WorkspaceSettings(1, ["Default", OtherId]));
            Assert.True(Store.Save(new PenSettings()));
            Assert.True(new PenSettingsStore(Workspace.SettingsPath(OtherId)).Save(new PenSettings { Title = "Other" }));
            Moves = new PenMoveService(Desktop);
            Assert.Null(Moves.Recover([]).Error);
            Assert.Null(Moves.PrepareTransferStorage(create: true));
            File.WriteAllText(Path.Combine(Desktop, "Keep.txt"), "keep");
            Service = new PenDeletionService(Workspace, Desktop);
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
