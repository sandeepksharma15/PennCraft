using PennCraft.Desktop;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenGeometryTests
{
    [Theory]
    [InlineData("N", 600, 290, 100, 110)]
    [InlineData("NE", 620, 290, 100, 110)]
    [InlineData("E", 620, 300, 100, 100)]
    [InlineData("SE", 620, 310, 100, 100)]
    [InlineData("S", 600, 310, 100, 100)]
    [InlineData("SW", 580, 310, 120, 100)]
    [InlineData("W", 580, 300, 120, 100)]
    [InlineData("NW", 580, 290, 120, 110)]
    public void AllEdgesResizeCorrectly(string edge, double width, double height, double left, double top)
    {
        var resized = PenGeometry.Resize(new(), edge, 20, 10);
        Assert.Equal(width, resized.Width);
        Assert.Equal(height, resized.Height);
        Assert.Equal(left, resized.Left);
        Assert.Equal(top, resized.Top);
    }

    [Fact]
    public void NorthWestMinimumKeepsOppositeCornerFixed()
    {
        var before = new PenSettings();
        var after = PenGeometry.Resize(before, "NW", 10000, 10000);
        Assert.Equal(PenSettings.MinimumWidth, after.Width);
        Assert.Equal(PenSettings.MinimumHeight, after.Height);
        Assert.Equal(before.Left + before.Width, after.Left + after.Width);
        Assert.Equal(before.Top + before.Height, after.Top + after.Height);
    }
}
