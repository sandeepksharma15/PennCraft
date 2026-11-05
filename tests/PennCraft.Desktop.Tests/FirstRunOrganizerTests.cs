using System.IO;
using PennCraft.Desktop.Items;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class FirstRunOrganizerTests
{
    [Theory]
    [InlineData("FILE.PDF", FileAttributes.Normal, "Documents")]
    [InlineData("Tool.lnk", FileAttributes.Normal, "Programs")]
    [InlineData("Folder.pdf", FileAttributes.Directory, "Folders")]
    [InlineData("Other.txt", FileAttributes.Normal, null)]
    [InlineData("Folder", FileAttributes.Hidden | FileAttributes.Directory, null)]
    [InlineData("Link", FileAttributes.ReparsePoint | FileAttributes.Directory, null)]
    [InlineData("System.pdf", FileAttributes.System, null)]
    public void FixedClassificationIsShallowAndExcludesSpecialItems(string path, FileAttributes attributes, string? category) =>
        Assert.Equal(category, FirstRunOrganizer.Category(path, attributes));

    [Fact]
    public void FreshWorkspaceOrganizesOnceAndNeverMovesNewItemsOnLaterRuns()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Desktop, "Book.PDF"), "pdf bytes");
        File.WriteAllText(Path.Combine(fixture.Desktop, "App.lnk"), "shortcut bytes");
        File.WriteAllText(Path.Combine(fixture.Desktop, "Other.txt"), "keep");
        var folder = Directory.CreateDirectory(Path.Combine(fixture.Desktop, "Project")).FullName;
        File.WriteAllText(Path.Combine(folder, "nested.pdf"), "nested");
        var report = new FirstRunOrganizer(fixture.Store, fixture.Desktop).Run();
        Assert.Equal(3, report!.Moved);
        Assert.Empty(report.Skipped);
        var pens = fixture.Store.Load().PenIds.Select(id => new PenSettingsStore(fixture.Store.SettingsPath(id)).Load()).ToArray();
        Assert.Equal(3, pens.Length);
        Assert.Equal("pdf bytes", File.ReadAllText(Assert.Single(pens.Single(pen => pen.Title == "Documents").ItemPaths)));
        Assert.Equal("shortcut bytes", File.ReadAllText(Assert.Single(pens.Single(pen => pen.Title == "Programs").ItemPaths)));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(Assert.Single(pens.Single(pen => pen.Title == "Folders").ItemPaths), "nested.pdf")));
        Assert.True(File.Exists(Path.Combine(fixture.Desktop, "Other.txt")));
        var later = Path.Combine(fixture.Desktop, "Later.pdf");
        File.WriteAllText(later, "leave");
        Assert.Null(new FirstRunOrganizer(fixture.Store, fixture.Desktop).Run());
        Assert.True(File.Exists(later));
    }

    [Fact]
    public void UnreadableInventoryEntryIsSkippedWithoutStoppingOtherMoves()
    {
        using var fixture = new Fixture();
        var blocked = Path.Combine(fixture.Desktop, "Blocked.pdf");
        var readable = Path.Combine(fixture.Desktop, "Readable.pdf");
        File.WriteAllText(blocked, "keep");
        File.WriteAllText(readable, "move");
        var report = new FirstRunOrganizer(fixture.Store, fixture.Desktop,
            path => path == blocked ? throw new UnauthorizedAccessException() : File.GetAttributes(path)).Run();
        Assert.Equal(1, report!.Moved);
        Assert.Equal(blocked, Assert.Single(report.Skipped));
        Assert.Equal("keep", File.ReadAllText(blocked));
        Assert.False(File.Exists(readable));
    }

    [Theory]
    [InlineData("workspace")]
    [InlineData("legacy")]
    [InlineData("orphan")]
    [InlineData("storage")]
    public void ExistingAndLegacyWorkspacesAreNeverAutomaticallyOrganized(string kind)
    {
        using var fixture = new Fixture();
        var source = Path.Combine(fixture.Desktop, "Keep.pdf");
        File.WriteAllText(source, "keep");
        if (kind == "workspace") fixture.Store.Save(new WorkspaceSettings(1, []));
        if (kind == "legacy") Assert.True(new PenSettingsStore(fixture.Store.SettingsPath("Default")).Save(new PenSettings { IsVisible = false }));
        if (kind == "orphan") Assert.True(new PenSettingsStore(fixture.Store.SettingsPath(Guid.NewGuid().ToString("N"))).Save(new PenSettings()));
        if (kind == "storage") Directory.CreateDirectory(Path.Combine(fixture.Desktop, ".PennCraft"));
        Assert.Null(new FirstRunOrganizer(fixture.Store, fixture.Desktop).Run());
        Assert.Equal("keep", File.ReadAllText(source));
    }

    private sealed class Fixture : IDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "PennCraft-first-run", Guid.NewGuid().ToString("N"));
        public string Desktop { get; }
        public WorkspaceStore Store { get; }
        public Fixture()
        {
            Desktop = Directory.CreateDirectory(Path.Combine(Root, "Desktop")).FullName;
            Store = new WorkspaceStore(Path.Combine(Root, "Settings"));
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
