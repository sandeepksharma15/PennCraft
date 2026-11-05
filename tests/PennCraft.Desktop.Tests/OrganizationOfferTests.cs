using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Items;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class OrganizationOfferTests
{
    [Fact]
    public void OnlyAnEntirelyEmptyHiddenWorkspaceQualifies()
    {
        var hidden = new PenSettings { IsVisible = false };
        Assert.True(OrganizationOfferPolicy.IsEligible([]));
        Assert.True(OrganizationOfferPolicy.IsEligible([hidden, hidden with { Title = "Other" }]));
        Assert.False(OrganizationOfferPolicy.IsEligible([hidden, new PenSettings()]));
        Assert.False(OrganizationOfferPolicy.IsEligible([hidden with { ItemPaths = [Path.Combine(Path.GetTempPath(), "Unavailable.pdf")] }]));
        Assert.False(OrganizationOfferPolicy.IsEligible([hidden with { ItemPaths = [VirtualDesktopItem.RecycleBin] }]));
        Assert.True(OrganizationOfferPolicy.IsEligible([hidden with { PortalFolder = Path.GetTempPath() }]));
        Assert.False(OrganizationOfferPolicy.IsEligible([new PenSettings { PortalFolder = Path.GetTempPath() }]));
    }

    [Fact]
    public void DismissalPersistsAndLegacySettingsRemainEligibleForAnOffer()
    {
        WithWorkspace((root, workspace) =>
        {
            var index = Path.Combine(root, "Settings", "workspace.json");
            workspace.Save(new WorkspaceSettings(1, ["Default"]));
            Assert.False(workspace.Load().OrganizationOfferDismissed);
            var dismissed = workspace.Load() with { OrganizationOfferDismissed = true };
            workspace.Save(dismissed);
            Assert.True(new WorkspaceStore(Path.Combine(root, "Settings")).Load().OrganizationOfferDismissed);
            workspace.Save(dismissed with { OrganizationOfferDismissed = false });
            Assert.False(workspace.Load().OrganizationOfferDismissed);
            File.WriteAllText(index, JsonSerializer.Serialize(new { Version = 1, PenIds = new[] { "Default" }, CreationEnabled = true }));
            Assert.False(workspace.Load().OrganizationOfferDismissed);
        });
    }

    [Fact]
    public void PreviewCountsOnlySupportedItemsWithoutCreatingOrMovingAnything()
    {
        WithWorkspace((root, workspace) =>
        {
            var desktop = Directory.CreateDirectory(Path.Combine(root, "Desktop")).FullName;
            var pdf = Path.Combine(desktop, "Book.pdf");
            File.WriteAllText(pdf, "keep pdf");
            File.WriteAllText(Path.Combine(desktop, "App.lnk"), "keep shortcut");
            File.WriteAllText(Path.Combine(desktop, "Other.txt"), "keep other");
            Directory.CreateDirectory(Path.Combine(desktop, "Project"));
            var storage = Directory.CreateDirectory(Path.Combine(desktop, ".PennCraft")).FullName;
            File.SetAttributes(storage, FileAttributes.Hidden);
            var preview = new FirstRunOrganizer(workspace, desktop).Preview();
            Assert.Equal(3, preview.Count);
            Assert.Equal(pdf, Assert.Single(preview.Groups["Documents"]));
            Assert.Single(preview.Groups["Programs"]);
            Assert.Single(preview.Groups["Folders"]);
            Assert.Equal("keep pdf", File.ReadAllText(pdf));
            Assert.False(Directory.Exists(Path.Combine(root, "Settings")));
        });
    }

    private static void WithWorkspace(Action<string, WorkspaceStore> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-offer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root, new WorkspaceStore(Path.Combine(root, "Settings"))); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
