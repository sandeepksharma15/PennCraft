using System.IO;
using System.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenItemScaleTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(0.9)]
    [InlineData(2)]
    public void UnsupportedScalesUseDefault(double value) =>
        Assert.Equal(1, new PenSettings { ItemScale = value }.Normalize().ItemScale);

    [Fact]
    public void LegacySettingsWithoutScaleRemainAtDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-scale", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "pen.json");
            File.WriteAllText(file, "{\"Title\":\"Legacy\",\"Height\":260}");
            var settings = new PenSettingsStore(file).Load();
            Assert.Equal(1, settings.ItemScale);
            Assert.Equal("Legacy", settings.Title);
            Assert.Equal(260, settings.Height);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(0.75, 1)]
    [InlineData(1, 1)]
    [InlineData(1.25, 1)]
    [InlineData(1.5, 1)]
    [InlineData(0.75, 1.5)]
    [InlineData(1.5, 1.5)]
    public void LayoutPreservesSixScaledColumnsAndThreeRows(double itemScale, double displayScale)
    {
        var width = PenLayoutMetrics.CommonWidth(20, itemScale);
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 3840, 2160), displayScale, width,
            [new("scaled", new Rect(0, 0, 600, 300), 18, 60, ItemScale: itemScale)]);
        Assert.Null(plan.Error);
        var bounds = plan.Bounds["scaled"];
        Assert.True(bounds.Width / displayScale >= 6 * PenLayoutMetrics.CellWidth * itemScale + 40);
        Assert.True(bounds.Height / displayScale >= 3 * PenLayoutMetrics.CellHeight * itemScale + 60);
    }

    [Fact]
    public void MixedScaleLayoutUsesActualColumnCountAndFailsWithoutPartialResults()
    {
        var width = PenLayoutMetrics.CommonWidth(20, 1.5);
        var columns = PenLayoutMetrics.ColumnCount(width, 20, 0.75);
        Assert.True(columns >= 12);
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 3840, 2160), 1, width,
            [new("small", new Rect(0, 0, 600, 300), columns * 4, 60, ItemScale: 0.75, ColumnCount: columns),
             new("large", new Rect(0, 10, 600, 300), 18, 60, ItemScale: 1.5)]);
        Assert.Null(plan.Error);
        var grid = ScreenGrid.AtScale(new Point(), 1);
        Assert.Equal(grid.LengthUp(4 * PenLayoutMetrics.CellHeight * 0.75 + 60), plan.Bounds["small"].Height);
        Assert.Equal(plan.Bounds["small"].Width, plan.Bounds["large"].Width);
        var tooSmall = PenLayoutPlanner.Plan(new Rect(0, 0, 800, 600), 1, width,
            [new("large", new Rect(0, 0, 600, 300), 18, 60, ItemScale: 1.5)]);
        Assert.NotNull(tooSmall.Error);
        Assert.Empty(tooSmall.Bounds);
    }
}
