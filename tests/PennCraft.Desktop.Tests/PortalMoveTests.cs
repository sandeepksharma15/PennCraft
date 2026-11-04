using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PortalMoveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DesktopAndPortalMovesPreserveContents(bool folder)
    {
        using var fixture = new Fixture();
        var source = Path.Combine(fixture.Desktop, folder ? "Folder" : "Item.lnk");
        if (folder) { Directory.CreateDirectory(source); File.WriteAllText(Path.Combine(source, "child.txt"), "nested"); }
        else File.WriteAllText(source, "shortcut bytes");
        Assert.Null(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            NativeFileMove.Identity(source), Rename));
        var destination = Path.Combine(fixture.Folder, Path.GetFileName(source));
        Assert.False(PenMembership.Exists(source));
        Assert.Equal(folder ? "nested" : "shortcut bytes", File.ReadAllText(folder ? Path.Combine(destination, "child.txt") : destination));
        Assert.Empty(fixture.PortalStore.Load().ItemPaths);
        Assert.Null(fixture.Service.Move(fixture.PortalId, null, destination, fixture.Desktop,
            NativeFileMove.Identity(destination), Rename));
        Assert.True(PenMembership.Exists(source));
    }

    [Fact]
    public void PenAndPortalTransferCommitsMembershipInBothDirections()
    {
        using var fixture = new Fixture();
        var desktopFile = Path.Combine(fixture.Desktop, "File.txt");
        File.WriteAllText(desktopFile, "original");
        var outcome = fixture.PenMoves.MoveInto(desktopFile);
        Assert.True(outcome.Moved);
        Assert.Null(fixture.PenMoves.Acknowledge([outcome.OperationId!.Value]));
        Assert.True(fixture.PenStore.Save(new PenSettings { ItemPaths = [outcome.Destination] }));
        Assert.Null(fixture.Service.Move("Default", fixture.PortalId, outcome.Destination, fixture.Folder,
            NativeFileMove.Identity(outcome.Destination), Rename));
        Assert.Empty(fixture.PenStore.Load().ItemPaths);
        var portalFile = Path.Combine(fixture.Folder, "File.txt");
        Assert.Null(fixture.Service.Move(fixture.PortalId, "Default", portalFile, fixture.PenMoves.StorageDirectory,
            NativeFileMove.Identity(portalFile), Rename));
        Assert.Equal(outcome.Destination, Assert.Single(fixture.PenStore.Load().ItemPaths));
        Assert.Equal("original", File.ReadAllText(outcome.Destination));
    }

    [Fact]
    public void ConflictAndStaleIdentityDoNotCallMover()
    {
        using var fixture = new Fixture();
        var source = fixture.Source();
        File.WriteAllText(Path.Combine(fixture.Folder, "Item.txt"), "keep");
        Assert.NotNull(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            NativeFileMove.Identity(source), (_, _) => Assert.Fail("Must not move")));
        Assert.NotNull(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            "stale", (_, _) => Assert.Fail("Must not move")));
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(fixture.Folder, "Item.txt")));
    }

    [Fact]
    public void CancelBeforeMoveRetainsSourceAndClearsIntent()
    {
        using var fixture = new Fixture();
        var source = fixture.Source();
        Assert.NotNull(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            NativeFileMove.Identity(source), (_, _) => throw new OperationCanceledException()));
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(fixture.Service.JournalPath));
        Assert.Null(fixture.Service.BlockedReason);
    }

    [Fact]
    public void FailureAfterRenameRecoversVerifiedDestinationWithoutReplay()
    {
        using var fixture = new Fixture();
        var source = fixture.Source();
        Assert.NotNull(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            NativeFileMove.Identity(source), (from, to) => { File.Move(from, to); throw new IOException("Interrupted"); }));
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(fixture.Folder, "Item.txt")));
        Assert.Null(fixture.Service.Recover());
        Assert.False(File.Exists(fixture.Service.JournalPath));
    }

    [Fact]
    public void PartialCrossVolumeStyleCopyIsRetainedAndBlocked()
    {
        using var fixture = new Fixture();
        var source = fixture.Source();
        Assert.NotNull(fixture.Service.Move(null, fixture.PortalId, source, fixture.Folder,
            NativeFileMove.Identity(source), (from, to) => { File.Copy(from, to); throw new OperationCanceledException(); }));
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(fixture.Folder, "Item.txt")));
        Assert.NotNull(fixture.Service.BlockedReason);
        Assert.True(File.Exists(fixture.Service.JournalPath));
    }

    [Fact]
    public void PortalToPortalUsesBothLinkedFolders()
    {
        using var fixture = new Fixture();
        var second = Guid.NewGuid().ToString("N");
        fixture.Workspace.Save(new WorkspaceSettings(1, ["Default", fixture.PortalId, second]));
        var other = Directory.CreateDirectory(Path.Combine(fixture.Root, "Other")).FullName;
        Assert.True(new PenSettingsStore(fixture.Workspace.SettingsPath(second)).Save(new PenSettings { PortalFolder = other }));
        var source = Path.Combine(fixture.Folder, "Source.txt");
        File.WriteAllText(source, "data");
        Assert.Null(fixture.Service.Move(fixture.PortalId, second, source, other, NativeFileMove.Identity(source), Rename));
        Assert.Equal("data", File.ReadAllText(Path.Combine(other, "Source.txt")));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void CorruptAndOutOfScopeRecoveryRecordsFailClosed()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Service.JournalPath, "{broken");
        Assert.NotNull(fixture.Service.Recover());
        Assert.True(File.Exists(fixture.Service.JournalPath));
        var source = fixture.Source();
        File.WriteAllText(fixture.Service.JournalPath, JsonSerializer.Serialize(new PortalMoveIntent(
            1, null, fixture.PortalId, source, Path.Combine(fixture.Root, "Outside.txt"), NativeFileMove.Identity(source)!)));
        Assert.NotNull(new PortalMoveService(fixture.Workspace, fixture.Desktop).Recover());
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task WindowsShellMovesDisposableFileOnSta()
    {
        using var fixture = new Fixture();
        var source = fixture.Source();
        var destination = Path.Combine(fixture.Folder, "Item.txt");
        await ShellFileMove.OnSta(() => { ShellFileMove.Move(source, destination, 0); return true; });
        Assert.False(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(destination));
    }

    private static void Rename(string source, string destination)
    {
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "PennCraft-portal-moves", Guid.NewGuid().ToString("N"));
        public string Desktop { get; }
        public string Folder { get; }
        public string PortalId { get; } = Guid.NewGuid().ToString("N");
        public WorkspaceStore Workspace { get; }
        public PortalMoveService Service { get; }
        public PenMoveService PenMoves { get; }
        public PenSettingsStore PenStore => new(Workspace.SettingsPath("Default"));
        public PenSettingsStore PortalStore => new(Workspace.SettingsPath(PortalId));
        public Fixture()
        {
            Desktop = Directory.CreateDirectory(Path.Combine(Root, "Desktop")).FullName;
            Folder = Directory.CreateDirectory(Path.Combine(Root, "Linked")).FullName;
            Workspace = new WorkspaceStore(Path.Combine(Root, "Settings"));
            Workspace.Save(new WorkspaceSettings(1, ["Default", PortalId]));
            Assert.True(PenStore.Save(new PenSettings()));
            Assert.True(PortalStore.Save(new PenSettings { PortalFolder = Folder, IsVisible = false }));
            PenMoves = new PenMoveService(Desktop);
            Assert.Null(PenMoves.Recover([]).Error);
            Service = new PortalMoveService(Workspace, Desktop);
        }
        public string Source()
        {
            var source = Path.Combine(Desktop, "Item.txt");
            File.WriteAllText(source, "source");
            return source;
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
