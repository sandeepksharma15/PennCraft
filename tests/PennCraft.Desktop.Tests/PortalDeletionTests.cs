using System.IO;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PortalDeletionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExplicitPortalDeletionAndRecoveryNeverTouchLinkedFolder(int interrupt)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-portal-delete", Guid.NewGuid().ToString("N"));
        try
        {
            var desktop = Directory.CreateDirectory(Path.Combine(root, "Desktop")).FullName;
            var linked = Directory.CreateDirectory(Path.Combine(root, "Linked")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(linked, "Nested")).FullName;
            File.WriteAllText(Path.Combine(linked, "Keep.txt"), "keep root");
            File.WriteAllText(Path.Combine(nested, "Keep.txt"), "keep nested");
            var workspace = new WorkspaceStore(Path.Combine(root, "Settings"));
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            var file = workspace.SettingsPath("Default");
            Assert.True(new PenSettingsStore(file).Save(new PenSettings { PortalFolder = linked }));
            var service = new PenDeletionService(workspace, desktop, point =>
            {
                if ((int)point == interrupt) throw new IOException("Interrupted");
            });
            var result = service.Delete("Default", portal: true);
            if (interrupt < 0) Assert.True(result.Removed, result.Error);
            else Assert.False(result.Removed);
            Assert.Null(new PenDeletionService(workspace, desktop).Recover());
            Assert.Empty(workspace.Load().PenIds);
            Assert.False(File.Exists(file));
            Assert.Equal("keep root", File.ReadAllText(Path.Combine(linked, "Keep.txt")));
            Assert.Equal("keep nested", File.ReadAllText(Path.Combine(nested, "Keep.txt")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
