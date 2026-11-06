using System.IO;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class BetaReadinessTests
{
    [Fact]
    public void EmbeddedIconsSurviveTheirResourceStreamsAndVersionMatchesAssembly()
    {
        using var icon = AppInfo.CreateTrayIcon();
        Assert.Equal(32, icon.Width);
        Assert.Equal(32, icon.Height);
        using var bitmap = icon.ToBitmap();
        Assert.True(bitmap.GetPixel(16, 16).A > 0);
        var windowIcon = AppInfo.CreateWindowIcon();
        Assert.True(windowIcon.IsFrozen);
        Assert.True(windowIcon.PixelWidth >= 16);
        Assert.Equal(typeof(App).Assembly.GetName().Version!.ToString(3), AppInfo.Version.Split('-')[0]);
    }

    [Fact]
    public void SupportLinksRejectNonWebTargetsWithoutLaunchingAnything() =>
        Assert.NotNull(AppInfo.OpenWebsite("file:///private-file"));

    [Fact]
    public void StartingEmptyConsumesFirstRunWithoutMovingDesktopFiles()
    {
        WithRoot(root =>
        {
            var desktop = Path.Combine(root, "Desktop");
            Directory.CreateDirectory(desktop);
            var pdf = Path.Combine(desktop, "Keep.pdf");
            File.WriteAllText(pdf, "keep");
            var workspace = new WorkspaceStore(Path.Combine(root, "Settings"));
            FirstRunSetup.StartEmpty(workspace);
            Assert.False(workspace.IsUninitialized);
            var settings = new PenSettingsStore(workspace.SettingsPath("Default")).Load();
            Assert.Empty(settings.ItemPaths);
            Assert.True(settings.IsVisible);
            Assert.Null(new FirstRunOrganizer(workspace, desktop).Run());
            Assert.Equal("keep", File.ReadAllText(pdf));
            Assert.False(Directory.Exists(Path.Combine(desktop, ".PennCraft")));
        });
    }

    [Fact]
    public void SetupNeverOverwritesExistingUserSettings()
    {
        WithRoot(root =>
        {
            var workspace = new WorkspaceStore(root);
            var store = new PenSettingsStore(workspace.SettingsPath("Default"));
            Assert.True(store.Save(new PenSettings { Title = "Existing", IsVisible = false, IsFolded = true, ItemScale = 1.5 }));
            var original = File.ReadAllText(workspace.SettingsPath("Default"));
            FirstRunSetup.StartEmpty(workspace);
            Assert.Equal(original, File.ReadAllText(workspace.SettingsPath("Default")));
        });
    }

    private static void WithRoot(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-beta", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
