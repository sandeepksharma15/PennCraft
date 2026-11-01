using System.IO;
using PennCraft.Desktop;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenSettingsTests
{
    [Fact]
    public void RoundTripPreservesAllUserIntentIncludingHiddenState()
    {
        WithStore((store, _) =>
        {
            var settings = new PenSettings
            {
                Title = "Creative tools", Left = -450, Top = 220, Width = 420,
                Height = 180, BackgroundOpacity = 0.55, IsVisible = false
            };
            Assert.True(store.Save(settings));
            Assert.Equal(settings, store.Load());
            Assert.True(store.Save(settings with { IsVisible = true }));
            Assert.True(store.Load().IsVisible);
        });
    }

    [Fact]
    public void MalformedSettingsFallBackWithoutDeletingOriginal()
    {
        WithStore((store, path) =>
        {
            File.WriteAllText(path, "{broken");
            Assert.Equal(new PenSettings(), store.Load());
            Assert.Equal("{broken", File.ReadAllText(path));
        });
    }

    [Fact]
    public void NormalizationRejectsInvalidGeometryAndOpacity()
    {
        var result = new PenSettings
        {
            Title = "  ", Left = double.NaN, Top = double.PositiveInfinity,
            Width = -1, Height = 0, BackgroundOpacity = 12
        }.Normalize();
        Assert.Equal("Programs", result.Title);
        Assert.Equal(100, result.Left);
        Assert.Equal(100, result.Top);
        Assert.Equal(PenSettings.MinimumWidth, result.Width);
        Assert.Equal(PenSettings.MinimumHeight, result.Height);
        Assert.Equal(1, result.BackgroundOpacity);
    }

    [Fact]
    public void FailedSaveDoesNotReplaceExistingFile()
    {
        WithStore((store, path) =>
        {
            Assert.True(store.Save(new PenSettings { Title = "Original" }));
            Directory.CreateDirectory(path + ".tmp");
            Assert.False(store.Save(new PenSettings { Title = "Replacement" }));
            Assert.Equal("Original", store.Load().Title);
        });
    }

    private static void WithStore(Action<PenSettingsStore, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PennCraft-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "pen.json");
            test(new PenSettingsStore(path), path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
