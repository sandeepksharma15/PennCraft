using System.IO;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class SharedShortcutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmedSharedMovePreservesBytesAndReplacesExistingReference(bool referenceExists)
    {
        WithWorkspace((workspace, desktop, shared, storage) =>
        {
            var source = Path.Combine(shared, "App.lnk");
            File.WriteAllText(source, "shortcut bytes");
            var settings = new PenSettings { ItemPaths = referenceExists ? [source] : [] };
            var store = new PenSettingsStore(workspace.SettingsPath("Default"));
            Assert.True(store.Save(settings));
            var service = new PortalMoveService(workspace, desktop, shared);
            Assert.Null(service.Move(null, "Default", source, storage, NativeFileMove.Identity(source), File.Move));
            var destination = Path.Combine(storage, "App.lnk");
            Assert.False(File.Exists(source));
            Assert.Equal("shortcut bytes", File.ReadAllText(destination));
            Assert.Equal(destination, Assert.Single(store.Load().ItemPaths));
            Assert.Null(service.Recover());
        });
    }

    [Theory]
    [InlineData("App.exe")]
    [InlineData("Document.pdf")]
    [InlineData("Nested/App.lnk")]
    public void SharedSourceScopeRejectsAnythingExceptImmediateShortcuts(string relative)
    {
        WithWorkspace((workspace, desktop, shared, storage) =>
        {
            var source = Path.GetFullPath(Path.Combine(shared, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, "keep");
            var service = new PortalMoveService(workspace, desktop, shared);
            Assert.NotNull(service.Move(null, "Default", source, storage, NativeFileMove.Identity(source),
                (_, _) => Assert.Fail("Outside shared shortcut scope")));
            Assert.Equal("keep", File.ReadAllText(source));
        });
    }

    [Fact]
    public void CancellationAndNameConflictRetainOriginalSharedShortcut()
    {
        WithWorkspace((workspace, desktop, shared, storage) =>
        {
            var source = Path.Combine(shared, "App.lnk");
            File.WriteAllText(source, "shared");
            var service = new PortalMoveService(workspace, desktop, shared);
            Assert.NotNull(service.Move(null, "Default", source, storage, NativeFileMove.Identity(source),
                (_, _) => throw new OperationCanceledException()));
            Assert.True(File.Exists(source));
            Assert.Null(service.BlockedReason);
            var destination = Path.Combine(storage, "App.lnk");
            File.WriteAllText(destination, "existing");
            Assert.NotNull(service.Move(null, "Default", source, storage, NativeFileMove.Identity(source),
                (_, _) => Assert.Fail("Conflict must not overwrite")));
            Assert.Equal("shared", File.ReadAllText(source));
            Assert.Equal("existing", File.ReadAllText(destination));
        });
    }

    [Fact]
    public void SharedEnumerationAndClassificationAreShallowAndReadOnly()
    {
        WithWorkspace((_, _, shared, _) =>
        {
            var link = Path.Combine(shared, "App.LNK");
            File.WriteAllText(link, "keep");
            File.WriteAllText(Path.Combine(shared, "Other.txt"), "keep");
            var nested = Directory.CreateDirectory(Path.Combine(shared, "Folder")).FullName;
            File.WriteAllText(Path.Combine(nested, "Other.lnk"), "keep");
            Assert.Equal(link, Assert.Single(SharedDesktopShortcut.Enumerate(shared)));
            Assert.True(SharedDesktopShortcut.Is(link, shared));
            Assert.False(SharedDesktopShortcut.Is(Path.Combine(nested, "Other.lnk"), shared));
            Assert.Equal("keep", File.ReadAllText(link));
        });
    }

    private static void WithWorkspace(Action<WorkspaceStore, string, string, string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-shared", Guid.NewGuid().ToString("N"));
        try
        {
            var desktop = Directory.CreateDirectory(Path.Combine(root, "Desktop")).FullName;
            var shared = Directory.CreateDirectory(Path.Combine(root, "Shared")).FullName;
            var workspace = new WorkspaceStore(Path.Combine(root, "Settings"));
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            Assert.True(new PenSettingsStore(workspace.SettingsPath("Default")).Save(new PenSettings()));
            var moves = new PenMoveService(desktop);
            Assert.Null(moves.Recover([]).Error);
            Assert.Null(moves.PrepareTransferStorage(create: true));
            action(workspace, desktop, shared, moves.StorageDirectory);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
