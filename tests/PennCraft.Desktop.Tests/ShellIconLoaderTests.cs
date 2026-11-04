using System.IO;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class ShellIconLoaderTests
{
    [Fact]
    public async Task NativeIconIsFrozenAndSourceFileIsPreserved()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PennCraft-icon-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "Keep this file");
        try
        {
            using var loader = new ShellIconLoader();
            var icon = await loader.LoadAsync(path).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.NotNull(icon);
            Assert.True(icon.IsFrozen);
            Assert.True(icon.PixelWidth > 0);
            Assert.Equal("Keep this file", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
