using System.IO;
using System.Text.Json;
using System.Windows;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenTransferTests
{
    [Theory]
    [InlineData("Notes.txt")]
    [InlineData("Shortcut.lnk")]
    public void StoredItemTransfersBytesIdentityAndBothMemberships(string name)
    {
        using var fixture = new Fixture();
        var path = fixture.StoreItem(name, "Keep");
        var identity = NativeFileMove.Identity(path);
        var result = fixture.Transfer(path, identity);
        Assert.True(result.Completed, result.Error);
        var destination = Path.Combine(fixture.TargetMoves.StorageDirectory, name);
        Assert.False(File.Exists(path));
        Assert.Equal("Keep", File.ReadAllText(destination));
        Assert.Equal(identity, NativeFileMove.Identity(destination));
        Assert.Empty(fixture.SourceStore().Load().ItemPaths);
        Assert.Equal(new[] { destination }, fixture.TargetStore().Load().ItemPaths);
        Assert.Equal("Source tools", fixture.SourceStore().Load().Title);
        Assert.Equal(-300, fixture.SourceStore().Load().Left);
        Assert.Equal("Destination notes", fixture.TargetStore().Load().Title);
        Assert.False(fixture.TargetStore().Load().IsVisible);
        Assert.Null(fixture.NewService().Recover());
    }

    [Fact]
    public void FolderTransfersAsOneItemWithoutChangingItsContents()
    {
        using var fixture = new Fixture();
        var desktopFolder = Path.Combine(fixture.Desktop, "Work");
        Directory.CreateDirectory(Path.Combine(desktopFolder, "Nested"));
        File.WriteAllText(Path.Combine(desktopFolder, "Nested", "Keep.txt"), "Keep");
        var into = fixture.SourceMoves.MoveInto(desktopFolder);
        Assert.True(into.Moved, into.Error);
        Assert.Null(fixture.SourceMoves.Acknowledge([into.OperationId!.Value]));
        fixture.SetSource(into.Destination);
        var result = fixture.Transfer(into.Destination, NativeFileMove.Identity(into.Destination));
        Assert.True(result.Completed, result.Error);
        var target = Path.Combine(fixture.TargetMoves.StorageDirectory, "Work");
        Assert.False(Directory.Exists(into.Destination));
        Assert.Equal("Keep", File.ReadAllText(Path.Combine(target, "Nested", "Keep.txt")));
    }

    [Fact]
    public void ReferencesTransferMembershipWithoutMovingTheOriginal()
    {
        using var fixture = new Fixture();
        var reference = Path.Combine(fixture.Root, "External.txt");
        File.WriteAllText(reference, "External");
        fixture.SetSource(reference);
        var result = fixture.Transfer(reference, NativeFileMove.Identity(reference));
        Assert.True(result.Completed, result.Error);
        Assert.Empty(fixture.SourceStore().Load().ItemPaths);
        Assert.Equal(new[] { reference }, fixture.TargetStore().Load().ItemPaths);
        Assert.Equal("External", File.ReadAllText(reference));
        Assert.False(Directory.Exists(fixture.TargetMoves.StorageDirectory));
    }

    [Fact]
    public void DestinationConflictRetainsBothOriginalItems()
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Same.txt", "Source");
        var desktop = Path.Combine(fixture.Desktop, "same.txt");
        File.WriteAllText(desktop, "Destination");
        var into = fixture.TargetMoves.MoveInto(desktop);
        Assert.True(into.Moved, into.Error);
        Assert.Null(fixture.TargetMoves.Acknowledge([into.OperationId!.Value]));
        fixture.Target = fixture.Target with { ItemPaths = [into.Destination] };
        Assert.True(fixture.TargetStore().Save(fixture.Target));
        var result = fixture.Transfer(source, NativeFileMove.Identity(source));
        Assert.False(result.Completed);
        Assert.False(result.NeedsRecovery);
        Assert.Equal("Source", File.ReadAllText(source));
        Assert.Equal("Destination", File.ReadAllText(into.Destination));
        Assert.Equal(new[] { source }, fixture.SourceStore().Load().ItemPaths);
        Assert.Equal(new[] { into.Destination }, fixture.TargetStore().Load().ItemPaths);
    }

    [Fact]
    public void DuplicateReferenceAndSamePenDropsDoNotRemoveSourceMembership()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "Reference.txt");
        File.WriteAllText(path, "Keep");
        fixture.SetSource(path);
        fixture.Target = fixture.Target with { ItemPaths = [path] };
        Assert.True(fixture.TargetStore().Save(fixture.Target));
        Assert.False(fixture.Transfer(path, NativeFileMove.Identity(path)).Completed);
        var same = fixture.NewService().Transfer("Default", "Default", path, NativeFileMove.Identity(path),
            fixture.Source, fixture.Source, fixture.SourceMoves, fixture.SourceMoves);
        Assert.False(same.Completed);
        Assert.Equal(new[] { path }, fixture.SourceStore().Load().ItemPaths);
        Assert.Equal("Keep", File.ReadAllText(path));
    }

    [Theory]
    [InlineData((int)TransferCheckpoint.IntentSaved)]
    [InlineData((int)TransferCheckpoint.RenameApplied)]
    [InlineData((int)TransferCheckpoint.TargetSaved)]
    [InlineData((int)TransferCheckpoint.SourceSaved)]
    public void EveryCommitBoundaryRecoversWithoutReplayingPhysicalMoves(int point)
    {
        var checkpoint = (TransferCheckpoint)point;
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Recover.txt", "Keep");
        var identity = NativeFileMove.Identity(source);
        var service = fixture.NewService(stage => { if (stage == checkpoint) throw new IOException("Simulated interruption"); });
        var result = fixture.Transfer(source, identity, service);
        Assert.False(result.Completed);
        Assert.True(result.NeedsRecovery);
        Assert.NotNull(fixture.NewService().JournalPath);
        Assert.Null(fixture.NewService().Recover());
        if (checkpoint == TransferCheckpoint.IntentSaved)
        {
            Assert.True(File.Exists(source));
            Assert.Equal(new[] { source }, fixture.SourceStore().Load().ItemPaths);
            Assert.Empty(fixture.TargetStore().Load().ItemPaths);
        }
        else
        {
            var destination = Path.Combine(fixture.TargetMoves.StorageDirectory, "Recover.txt");
            Assert.False(File.Exists(source));
            Assert.Equal(identity, NativeFileMove.Identity(destination));
            Assert.Empty(fixture.SourceStore().Load().ItemPaths);
            Assert.Equal(new[] { destination }, fixture.TargetStore().Load().ItemPaths);
            Assert.Equal("Keep", File.ReadAllText(destination));
        }
        Assert.Null(fixture.NewService().Recover()); // Idempotent after acknowledgment.
    }

    [Fact]
    public void PreparedReferenceTransferCompletesBothMembershipsOnRestart()
    {
        using var fixture = new Fixture();
        var reference = Path.Combine(fixture.Root, "Missing reference.txt");
        fixture.SetSource(reference);
        var service = fixture.NewService(stage => { if (stage == TransferCheckpoint.IntentSaved) throw new IOException("Interrupted"); });
        Assert.True(fixture.Transfer(reference, null, service).NeedsRecovery);
        Assert.Null(fixture.NewService().Recover());
        Assert.Empty(fixture.SourceStore().Load().ItemPaths);
        Assert.Equal(new[] { reference }, fixture.TargetStore().Load().ItemPaths);
        Assert.False(File.Exists(reference));
    }

    [Fact]
    public void LockedSourceIsRetainedAndUnappliedIntentCanRecover()
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Locked.txt", "Keep");
        var identity = NativeFileMove.Identity(source);
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var result = fixture.Transfer(source, identity);
            Assert.False(result.Completed);
            Assert.True(result.NeedsRecovery);
        }
        Assert.Equal("Keep", File.ReadAllText(source));
        Assert.Null(fixture.NewService().Recover());
        Assert.Equal(new[] { source }, fixture.SourceStore().Load().ItemPaths);
        Assert.Empty(fixture.TargetStore().Load().ItemPaths);
    }

    [Fact]
    public void SettingsPreflightFailureDoesNotMoveTheFile()
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Keep.txt", "Keep");
        var identity = NativeFileMove.Identity(source);
        using (var locked = new FileStream(fixture.Workspace.SettingsPath(fixture.TargetId),
                   FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = fixture.Transfer(source, identity);
            Assert.False(result.Completed);
            Assert.False(result.NeedsRecovery);
        }
        Assert.Equal("Keep", File.ReadAllText(source));
        Assert.Empty(fixture.TargetStore().Load().ItemPaths);
    }

    [Fact]
    public void AmbiguousRecoveryPreservesBothFilesAndTheJournal()
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Ambiguous.txt", "Original");
        var identity = NativeFileMove.Identity(source);
        var service = fixture.NewService(stage => { if (stage == TransferCheckpoint.RenameApplied) throw new IOException("Interrupted"); });
        Assert.True(fixture.Transfer(source, identity, service).NeedsRecovery);
        File.WriteAllText(source, "New source item");
        var journal = File.ReadAllText(service.JournalPath);
        Assert.NotNull(fixture.NewService().Recover());
        Assert.Equal(journal, File.ReadAllText(service.JournalPath));
        Assert.Equal("New source item", File.ReadAllText(source));
        Assert.Equal("Original", File.ReadAllText(Path.Combine(fixture.TargetMoves.StorageDirectory, "Ambiguous.txt")));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"Version\":2,\"Pending\":null}")]
    [InlineData("{\"Version\":1}")]
    public void CorruptJournalIsRetainedAndBlocksTransfers(string text)
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Keep.txt", "Keep");
        var service = fixture.NewService();
        File.WriteAllText(service.JournalPath, text);
        Assert.NotNull(service.Recover());
        Assert.False(fixture.Transfer(source, NativeFileMove.Identity(source), service).Completed);
        Assert.Equal(text, File.ReadAllText(service.JournalPath));
        Assert.Equal("Keep", File.ReadAllText(source));
    }

    [Fact]
    public void InvalidJournalCannotMoveOrAdoptAnExternalFile()
    {
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "Outside.txt");
        File.WriteAllText(outside, "Keep");
        var intent = new PenTransferIntent(Guid.NewGuid(), "Default", fixture.TargetId, outside,
            Path.Combine(fixture.TargetMoves.StorageDirectory, "Outside.txt"), NativeFileMove.Identity(outside), true);
        var service = fixture.NewService();
        File.WriteAllText(service.JournalPath, JsonSerializer.Serialize(new PenTransferJournal(1, intent)));
        Assert.NotNull(service.Recover());
        Assert.Equal("Keep", File.ReadAllText(outside));
        Assert.Empty(fixture.TargetStore().Load().ItemPaths);
    }

    [Fact]
    public void ChangedSourceIdentityIsNotTransferred()
    {
        using var fixture = new Fixture();
        var source = fixture.StoreItem("Original.txt", "Original");
        var identity = NativeFileMove.Identity(source);
        var preserved = Path.Combine(fixture.SourceMoves.StorageDirectory, "Preserved.txt");
        File.Move(source, preserved);
        File.WriteAllText(source, "Replacement");
        var result = fixture.Transfer(source, identity);
        Assert.False(result.Completed);
        Assert.Equal("Replacement", File.ReadAllText(source));
        Assert.Equal("Original", File.ReadAllText(preserved));
        Assert.Empty(fixture.TargetStore().Load().ItemPaths);
    }

    [Fact]
    public void DragTokensExpireAndNeverExposeAFileDropPayload()
    {
        var registry = new PenDragRegistry();
        var path = Path.Combine(Path.GetTempPath(), "Reference.txt");
        var first = registry.Begin("Default", path, null);
        Assert.Same(first, registry.Resolve(first.Token));
        Assert.Null(registry.Resolve(Guid.NewGuid().ToString("N")));
        var data = DesktopItemDrag.CreateData(first);
        Assert.False(data.GetDataPresent(DataFormats.FileDrop, autoConvert: false));
        Assert.Equal(first.Token, data.GetData(PenDragRegistry.Format, autoConvert: false));
        registry.End(first);
        Assert.Null(registry.Resolve(first.Token));
        var second = registry.Begin("Default", path, null);
        Assert.Null(registry.Resolve(first.Token));
        second.Accepted = true;
        Assert.Null(registry.Resolve(second.Token));
        registry.End(second);
        Assert.Throws<ArgumentException>(() => registry.Begin("Default", "../relative", null));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "PennCraft-transfer-tests", Guid.NewGuid().ToString("N"));
        public string Desktop { get; }
        public string TargetId { get; } = Guid.NewGuid().ToString("N");
        public WorkspaceStore Workspace { get; }
        public PenMoveService SourceMoves { get; }
        public PenMoveService TargetMoves { get; }
        public PenSettings Source { get; private set; } = new() { Title = "Source tools", Left = -300 };
        public PenSettings Target { get; set; } = new() { Title = "Destination notes", IsVisible = false };
        public PenSettingsStore SourceStore() => new(Workspace.SettingsPath("Default"));
        public PenSettingsStore TargetStore() => new(Workspace.SettingsPath(TargetId));

        public Fixture()
        {
            Desktop = Path.Combine(Root, "Desktop");
            Directory.CreateDirectory(Desktop);
            Workspace = new WorkspaceStore(Path.Combine(Root, "Settings"));
            Workspace.Save(new WorkspaceSettings(1, ["Default", TargetId]));
            SourceMoves = new PenMoveService(Desktop);
            TargetMoves = new PenMoveService(Desktop, TargetId);
            Assert.Null(SourceMoves.Recover([]).Error);
            Assert.Null(TargetMoves.Recover([]).Error);
            Assert.True(SourceStore().Save(Source));
            Assert.True(TargetStore().Save(Target));
        }

        public void SetSource(string path)
        {
            Source = Source with { ItemPaths = [path], TrackedItems = ItemTracking.Capture([path], []) };
            Assert.True(SourceStore().Save(Source));
        }

        public string StoreItem(string name, string content)
        {
            var path = Path.Combine(Desktop, name);
            File.WriteAllText(path, content);
            var moved = SourceMoves.MoveInto(path);
            Assert.True(moved.Moved, moved.Error);
            Assert.Null(SourceMoves.Acknowledge([moved.OperationId!.Value]));
            SetSource(moved.Destination);
            return moved.Destination;
        }

        public PenTransferService NewService(Action<TransferCheckpoint>? checkpoint = null) => new(Workspace, Desktop, checkpoint);
        public PenTransferResult Transfer(string path, string? identity, PenTransferService? service = null) =>
            (service ?? NewService()).Transfer("Default", TargetId, path, identity, Source, Target, SourceMoves, TargetMoves);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
