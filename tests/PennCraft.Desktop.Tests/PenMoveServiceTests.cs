using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenMoveServiceTests
{
    [Fact]
    public void FileAndShortcutRoundTripPreserveBytesAndIdentity()
    {
        WithDesktop((desktop, service) =>
        {
            foreach (var name in new[] { "Notes.txt", "App.lnk" })
            {
                var source = Path.Combine(desktop, name);
                File.WriteAllBytes(source, [0, 1, 9, 200, 255]);
                var identity = NativeFileMove.Identity(source);
                var into = service.MoveInto(source);
                Assert.True(into.Moved, into.Error);
                Assert.False(File.Exists(source));
                Assert.Equal(identity, NativeFileMove.Identity(into.Destination));
                Assert.Equal(new byte[] { 0, 1, 9, 200, 255 }, File.ReadAllBytes(into.Destination));
                Assert.Null(service.Acknowledge([into.OperationId!.Value]));
                var back = service.MoveOut(into.Destination);
                Assert.True(back.Moved, back.Error);
                Assert.False(File.Exists(into.Destination));
                Assert.Equal(identity, NativeFileMove.Identity(source));
                Assert.Null(service.Acknowledge([back.OperationId!.Value]));
            }
        });
    }

    [Fact]
    public void FolderMovesAsOneItemWithoutChangingItsChildren()
    {
        WithDesktop((desktop, service) =>
        {
            var folder = Path.Combine(desktop, "Work");
            Directory.CreateDirectory(Path.Combine(folder, "Nested"));
            File.WriteAllText(Path.Combine(folder, "Nested", "Keep.txt"), "Keep");
            var moved = service.MoveInto(folder);
            Assert.True(moved.Moved, moved.Error);
            Assert.False(Directory.Exists(folder));
            Assert.Equal("Keep", File.ReadAllText(Path.Combine(moved.Destination, "Nested", "Keep.txt")));
            Assert.Null(service.Acknowledge([moved.OperationId!.Value]));
            var back = service.MoveOut(moved.Destination);
            Assert.True(back.Moved, back.Error);
            Assert.Equal("Keep", File.ReadAllText(Path.Combine(folder, "Nested", "Keep.txt")));
        });
    }

    [Fact]
    public void DestinationConflictNeverOverwritesEitherFile()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Same.txt");
            File.WriteAllText(source, "First");
            var moved = service.MoveInto(source);
            Assert.True(moved.Moved, moved.Error);
            Assert.Null(service.Acknowledge([moved.OperationId!.Value]));
            File.WriteAllText(source, "Second");
            Assert.NotNull(service.MoveInto(source).Error);
            Assert.NotNull(service.MoveOut(moved.Destination).Error);
            Assert.Equal("First", File.ReadAllText(moved.Destination));
            Assert.Equal("Second", File.ReadAllText(source));
        });
    }

    [Fact]
    public void LockedFileStaysInPlaceAndPendingIntentIsCancelledOnRecovery()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Locked.txt");
            File.WriteAllText(source, "Keep");
            using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Assert.NotNull(service.MoveInto(source).Error);
            Assert.Equal("Keep", File.ReadAllText(source));
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover([source]);
            Assert.Null(recovery.Error);
            Assert.Equal(new[] { source }, recovery.ItemPaths);
            Assert.Single(recovery.ResolvedIds);
            Assert.Null(restarted.Acknowledge(recovery.ResolvedIds));
        });
    }

    [Fact]
    public void RecoveryCommitsAppliedMoveEvenWhenSettingsWereNotSaved()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Recover.txt");
            File.WriteAllText(source, "Keep");
            var moved = service.MoveInto(source);
            Assert.True(moved.Moved, moved.Error);
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover([source]);
            Assert.Null(recovery.Error);
            Assert.Equal(new[] { moved.Destination }, recovery.ItemPaths);
            Assert.Equal(new[] { moved.OperationId!.Value }, recovery.ResolvedIds);
            // Retrying before acknowledgment is idempotent, including already-saved settings.
            var again = new PenMoveService(desktop).Recover(recovery.ItemPaths);
            Assert.Equal(recovery.ItemPaths, again.ItemPaths);
            Assert.Null(restarted.Acknowledge(recovery.ResolvedIds));
            Assert.Empty(MoveJournalFile.Read(restarted.JournalPath));
        });
    }

    [Fact]
    public void RecoveryRemovesMembershipAfterInterruptedMoveOut()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Return.txt");
            File.WriteAllText(source, "Keep");
            var into = service.MoveInto(source);
            Assert.Null(service.Acknowledge([into.OperationId!.Value]));
            var back = service.MoveOut(into.Destination);
            Assert.True(back.Moved, back.Error);
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover([into.Destination]);
            Assert.Null(recovery.Error);
            Assert.Empty(recovery.ItemPaths);
            Assert.Equal("Keep", File.ReadAllText(source));
            Assert.Null(restarted.Acknowledge(recovery.ResolvedIds));
        });
    }

    [Fact]
    public void AmbiguousRecoveryRetainsBothFilesAndBlocksFurtherMoves()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Ambiguous.txt");
            File.WriteAllText(source, "Moved");
            var into = service.MoveInto(source);
            File.WriteAllText(source, "New desktop item");
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover([source]);
            Assert.NotNull(recovery.Error);
            Assert.Empty(recovery.ResolvedIds);
            Assert.NotNull(restarted.MoveOut(into.Destination).Error);
            Assert.Equal("Moved", File.ReadAllText(into.Destination));
            Assert.Equal("New desktop item", File.ReadAllText(source));
        });
    }

    [Fact]
    public void WrongDestinationIdentityIsNotAdopted()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Identity.txt");
            File.WriteAllText(source, "Original");
            var into = service.MoveInto(source);
            File.Move(into.Destination, Path.Combine(desktop, "Preserved original.txt"));
            File.WriteAllText(into.Destination, "Unrelated");
            var recovery = new PenMoveService(desktop).Recover([source]);
            Assert.NotNull(recovery.Error);
            Assert.Empty(recovery.ResolvedIds);
            Assert.Equal(new[] { source }, recovery.ItemPaths);
            Assert.Equal("Unrelated", File.ReadAllText(into.Destination));
        });
    }

    [Fact]
    public void CorruptJournalAndUnownedStorageNeverAuthorizeMoves()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Keep.txt");
            File.WriteAllText(source, "Keep");
            var into = service.MoveInto(source);
            Assert.Null(service.Acknowledge([into.OperationId!.Value]));
            File.WriteAllText(service.JournalPath, "{broken");
            var restarted = new PenMoveService(desktop);
            Assert.NotNull(restarted.Recover([into.Destination]).Error);
            Assert.NotNull(restarted.MoveOut(into.Destination).Error);
            Assert.Equal("{broken", File.ReadAllText(service.JournalPath));
        });
        WithDesktop((desktop, service) =>
        {
            Directory.CreateDirectory(Path.Combine(desktop, ".PennCraft"));
            var source = Path.Combine(desktop, "Keep.txt");
            File.WriteAllText(source, "Keep");
            Assert.NotNull(service.MoveInto(source).Error);
            Assert.False(File.Exists(Path.Combine(desktop, ".PennCraft", "owner.txt")));
            Assert.Equal("Keep", File.ReadAllText(source));
        });
    }

    [Fact]
    public void JournalWriteFailurePreventsPhysicalMove()
    {
        WithDesktop((desktop, service) =>
        {
            var seed = Path.Combine(desktop, "Seed.txt");
            File.WriteAllText(seed, "Seed");
            var initial = service.MoveInto(seed);
            Assert.Null(service.Acknowledge([initial.OperationId!.Value]));
            var source = Path.Combine(desktop, "Keep.txt");
            File.WriteAllText(source, "Keep");
            using (var locked = new FileStream(service.JournalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.NotNull(service.MoveInto(source).Error);
            Assert.Equal("Keep", File.ReadAllText(source));
            Assert.False(File.Exists(Path.Combine(service.StorageDirectory, "Keep.txt")));
            Assert.Empty(MoveJournalFile.Read(service.JournalPath));
        });
    }

    [Fact]
    public void ExternalItemsStayReferencesAndStorageCannotBeMovedIntoItself()
    {
        WithDesktop((desktop, service) =>
        {
            var outside = Path.Combine(Path.GetDirectoryName(desktop)!, "Outside.txt");
            File.WriteAllText(outside, "Outside");
            var result = service.MoveInto(outside);
            Assert.False(result.Moved);
            Assert.Null(result.Error);
            Assert.Equal(outside, result.Destination);
            Assert.Null(service.MoveOut(outside).Error);
            Assert.Equal("Outside", File.ReadAllText(outside));
            Assert.NotNull(service.MoveInto(Path.Combine(desktop, ".PennCraft")).Error);
        });
    }

    [Fact]
    public void InvalidJournalPathsCannotChangeMembershipOrMoveExternalFiles()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Seed.txt");
            File.WriteAllText(source, "Keep");
            var into = service.MoveInto(source);
            Assert.Null(service.Acknowledge([into.OperationId!.Value]));
            var invalid = new MoveEntry(Guid.NewGuid(), into.Destination, Path.Combine(Path.GetDirectoryName(desktop)!, "Outside.txt"),
                NativeFileMove.Identity(into.Destination), false);
            File.WriteAllText(service.JournalPath, JsonSerializer.Serialize(new MoveJournal(1, [invalid])));
            var recovery = new PenMoveService(desktop).Recover([into.Destination]);
            Assert.NotNull(recovery.Error);
            Assert.Equal(new[] { into.Destination }, recovery.ItemPaths);
            Assert.Equal("Keep", File.ReadAllText(into.Destination));
        });
    }

    [Fact]
    public void FailedSettingsSaveRetainsIntentAndRecoversTheCompletedRename()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Settings.txt");
            File.WriteAllText(source, "Keep");
            var settingsPath = Path.Combine(Path.GetDirectoryName(desktop)!, "pen.json");
            var store = new PenSettingsStore(settingsPath);
            Assert.True(store.Save(new PenSettings { ItemPaths = [source] }));
            var into = service.MoveInto(source);
            Assert.True(into.Moved, into.Error);
            using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.False(store.Save(new PenSettings { ItemPaths = [into.Destination] }));
            Assert.Single(MoveJournalFile.Read(service.JournalPath));
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover(store.Load().ItemPaths);
            Assert.Null(recovery.Error);
            Assert.Equal(new[] { into.Destination }, recovery.ItemPaths);
            Assert.True(store.Save(store.Load() with { ItemPaths = recovery.ItemPaths }));
            Assert.Null(restarted.Acknowledge(recovery.ResolvedIds));
            Assert.Empty(MoveJournalFile.Read(service.JournalPath));
            Assert.Equal("Keep", File.ReadAllText(into.Destination));
        });
    }

    [Fact]
    public void FailedJournalAcknowledgementRetainsRecoverableIntent()
    {
        WithDesktop((desktop, service) =>
        {
            var source = Path.Combine(desktop, "Ack.txt");
            File.WriteAllText(source, "Keep");
            var into = service.MoveInto(source);
            Assert.True(into.Moved, into.Error);
            using (var locked = new FileStream(service.JournalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.NotNull(service.Acknowledge([into.OperationId!.Value]));
            Assert.Single(MoveJournalFile.Read(service.JournalPath));
            var restarted = new PenMoveService(desktop);
            var recovery = restarted.Recover([into.Destination]);
            Assert.Null(recovery.Error);
            Assert.Equal(new[] { into.Destination }, recovery.ItemPaths);
            Assert.Null(restarted.Acknowledge(recovery.ResolvedIds));
            Assert.Empty(MoveJournalFile.Read(service.JournalPath));
            Assert.Equal("Keep", File.ReadAllText(into.Destination));
        });
    }

    private static void WithDesktop(Action<string, PenMoveService> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-move-tests", Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(root, "Desktop");
        Directory.CreateDirectory(desktop);
        try
        {
            var service = new PenMoveService(desktop);
            Assert.Null(service.Recover([]).Error);
            test(desktop, service);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
