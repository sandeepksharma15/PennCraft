using System.IO;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Text.Json;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class RecycleBinTests
{
    [Fact]
    public void NamespaceMembershipIsAllowlistedAndSurvivesPersistenceAndReconciliation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PennCraft-bin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new PenSettingsStore(Path.Combine(directory, "pen.json"));
            Assert.Null(PenMembership.NormalizePath("::{00000000-0000-0000-0000-000000000000}"));
            Assert.True(store.Save(new PenSettings { ItemPaths = [VirtualDesktopItem.RecycleBin.ToLowerInvariant()] }));
            var settings = store.Load();
            Assert.Equal(VirtualDesktopItem.RecycleBin, Assert.Single(settings.ItemPaths));
            Assert.True(PenMembership.Exists(VirtualDesktopItem.RecycleBin));
            Assert.Empty(ItemTracking.Capture(settings.ItemPaths, []));
            Assert.Equal(settings.ItemPaths, ItemTracking.Reconcile(settings.ItemPaths, []).Paths);
            Assert.Equal("Recycle Bin", new PenItem(VirtualDesktopItem.RecycleBin).DisplayName);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void RecycleBinTransfersBetweenPensAsMembershipAndMovesOutWithoutFilesystemMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-bin-transfer", Guid.NewGuid().ToString("N"));
        var desktop = Directory.CreateDirectory(Path.Combine(root, "Desktop")).FullName;
        try
        {
            var workspace = new WorkspaceStore(Path.Combine(root, "Settings"));
            var targetId = Guid.NewGuid().ToString("N");
            workspace.Save(new WorkspaceSettings(1, ["Default", targetId]));
            var source = new PenSettings { ItemPaths = [VirtualDesktopItem.RecycleBin] };
            var target = new PenSettings();
            Assert.True(new PenSettingsStore(workspace.SettingsPath("Default")).Save(source));
            Assert.True(new PenSettingsStore(workspace.SettingsPath(targetId)).Save(target));
            var sourceMoves = new PenMoveService(desktop);
            var targetMoves = new PenMoveService(desktop, targetId);
            Assert.Null(sourceMoves.Recover(source.ItemPaths).Error);
            Assert.Null(targetMoves.Recover([]).Error);
            var service = new PenTransferService(workspace, desktop);
            var result = service.Transfer("Default", targetId, VirtualDesktopItem.RecycleBin, null,
                source, target, sourceMoves, targetMoves);
            Assert.True(result.Completed, result.Error);
            Assert.Empty(result.Source.ItemPaths);
            Assert.Equal(VirtualDesktopItem.RecycleBin, Assert.Single(result.Target.ItemPaths));
            Assert.Null(service.Recover());
            var moveOut = targetMoves.MoveOut(VirtualDesktopItem.RecycleBin);
            Assert.Null(moveOut.Error);
            Assert.False(moveOut.Moved);
            Assert.Empty(Directory.EnumerateFileSystemEntries(desktop));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisibilityBaselineIsDurableAndRestoresExactly(bool initiallyHidden)
    {
        WithVisibility((visibility, setting, state) =>
        {
            setting.Values["NewStartPanel"] = initiallyHidden ? 1 : null;
            setting.Values["ClassicStartMenu"] = 0;
            visibility.Update(true);
            Assert.True(File.Exists(state));
            Assert.Equal(1, setting.Read("NewStartPanel"));
            Assert.Equal(1, setting.Read("ClassicStartMenu"));
            visibility.Update(true);
            new RecycleBinVisibility(state, setting).Restore(); // Simulate restart after an interrupted session.
            Assert.Equal(initiallyHidden ? 1 : (int?)null, setting.Read("NewStartPanel"));
            Assert.Equal(0, setting.Read("ClassicStartMenu"));
            Assert.False(File.Exists(state));
        });
    }

    [Fact]
    public void ExternalVisibilityChangeIsPreservedAndMalformedStateNeverChangesSettings()
    {
        WithVisibility((visibility, setting, state) =>
        {
            visibility.Update(true);
            setting.Values["NewStartPanel"] = 0;
            visibility.Update(true); // Polling must not undo an explicit Windows settings change.
            Assert.Equal(0, setting.Read("NewStartPanel"));
            visibility.Restore();
            Assert.Equal(0, setting.Read("NewStartPanel"));
            File.WriteAllText(state, "{broken");
            Assert.Throws<JsonException>(() => visibility.Update(true));
            Assert.Equal(0, setting.Read("NewStartPanel"));
            Assert.Equal("{broken", File.ReadAllText(state));
            File.WriteAllText(state, JsonSerializer.Serialize(new { Version = 1 }));
            Assert.Throws<JsonException>(() => visibility.Restore());
            Assert.Equal(0, setting.Read("NewStartPanel"));
        });
    }

    [Fact]
    public void MalformedShellDragDataIsRejectedBeforeNativeParsing()
    {
        Assert.False(VirtualItemDrop.ReadCida([]));
        Assert.False(VirtualItemDrop.ReadCida(new byte[16]));
        var invalid = new byte[20];
        invalid[0] = 1;
        invalid[4] = 255;
        invalid[8] = 255;
        Assert.False(VirtualItemDrop.ReadCida(invalid));
    }

    [Fact]
    public async Task WindowsSuppliesRecycleBinIcon()
    {
        using var loader = new ShellIconLoader();
        Assert.NotNull(await loader.LoadAsync(VirtualDesktopItem.RecycleBin));
    }

    [Fact]
    public async Task WindowsRecycleBinDragIsAcceptedButUnknownNamespaceIsNot()
    {
        Assert.True(await ShellFileMove.OnSta(() =>
        {
            Marshal.ThrowExceptionForHR(OleInitialize(0));
            nint pidl = 0;
            try
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(VirtualDesktopItem.RecycleBin, 0, out pidl, 0, out _));
                var length = (int)ILGetSize(pidl);
                var data = new byte[14 + length];
                BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 12);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 14);
                Marshal.Copy(pidl, data, 14, length);
                var accepted = VirtualItemDrop.ReadCida(data);
                data[0] = 2; // Multiple objects cannot masquerade as a single system reference.
                return accepted && !VirtualItemDrop.ReadCida(data);
            }
            finally
            {
                if (pidl != 0) Marshal.FreeCoTaskMem(pidl);
                OleUninitialize();
            }
        }));
    }

    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string path, nint bind, out nint pidl, uint mask, out uint attributes);
    [DllImport("shell32.dll")] private static extern uint ILGetSize(nint pidl);

    private static void WithVisibility(Action<RecycleBinVisibility, FakeSetting, string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PennCraft-bin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var state = Path.Combine(directory, "visibility.json");
            var setting = new FakeSetting();
            action(new RecycleBinVisibility(state, setting), setting, state);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class FakeSetting : IDesktopIconSetting
    {
        public Dictionary<string, int?> Values { get; } = new() { ["NewStartPanel"] = null, ["ClassicStartMenu"] = null };
        public int? Read(string panel) => Values[panel];
        public void Write(string panel, int? value) => Values[panel] = value;
        public void Refresh() { }
    }
}
