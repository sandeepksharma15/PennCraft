using System.IO;
using PennCraft.Desktop.Items;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PortalDirectoryTests
{
    [Fact]
    public void ReadsImmediateContentsAndReflectsExternalChangesWithoutMovingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var child = Directory.CreateDirectory(Path.Combine(root, "Child")).FullName;
            File.WriteAllText(Path.Combine(child, "nested.txt"), "nested");
            var file = Path.Combine(root, "a.txt");
            File.WriteAllText(file, "original");
            var snapshot = PortalDirectory.Read(root);
            Assert.Null(snapshot.Error);
            Assert.Equal(2, snapshot.Paths.Length);
            Assert.Contains(child, snapshot.Paths);
            Assert.Contains(file, snapshot.Paths);
            var renamed = Path.Combine(root, "renamed.txt");
            File.Move(file, renamed);
            Assert.Contains(renamed, PortalDirectory.Read(root).Paths);
            Assert.Equal("original", File.ReadAllText(renamed));
            File.Delete(renamed);
            Assert.Single(PortalDirectory.Read(root).Paths);
            var hidden = Path.Combine(root, "hidden.txt");
            File.WriteAllText(hidden, "hidden");
            File.SetAttributes(hidden, FileAttributes.Hidden);
            Assert.DoesNotContain(hidden, PortalDirectory.Read(root).Paths);
        }
        finally { Directory.Delete(root, recursive: true); }
        Assert.NotNull(PortalDirectory.Read(root).Error);
        Directory.CreateDirectory(root);
        try { Assert.Null(PortalDirectory.Read(root).Error); }
        finally { Directory.Delete(root); }
    }

    [Fact]
    public void RelativeFolderIsRejected() => Assert.NotNull(PortalDirectory.Read("relative").Error);
}
