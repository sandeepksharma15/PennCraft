using System.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class ScreenGridTests
{
    [Theory]
    [InlineData(1, 12)]
    [InlineData(1.25, 15)]
    [InlineData(1.5, 18)]
    [InlineData(1.75, 21)]
    [InlineData(2, 24)]
    [InlineData(1.1, 13)]
    public void GridUsesIntegerPhysicalPixelsAtTheChosenScale(double scale, double step) =>
        Assert.Equal(step, ScreenGrid.AtScale(new Point(), scale).Step);

    [Fact]
    public void DifferentStartingBordersReachTheSameScreenLines()
    {
        var grid = ScreenGrid.AtScale(new Point(), 1);
        var first = new PenGeometryDrag(new Rect(5, 7, 400, 200), new Point(15, 17), null);
        var second = new PenGeometryDrag(new Rect(19, 23, 400, 200), new Point(29, 33), null);
        var target = new Point(111, 211);
        Assert.Equal(first.Calculate(target, grid, new Size(240, 120), false),
            second.Calculate(target, grid, new Size(240, 120), false));
        Assert.Equal(new Point(96, 204), first.Calculate(target, grid, new Size(240, 120), false).Location);
    }

    [Fact]
    public void SmallPointerMovementsAccumulateWithoutUsingAlreadySnappedBounds()
    {
        var drag = new PenGeometryDrag(new Rect(96, 96, 400, 200), new Point(100, 100), null);
        var grid = ScreenGrid.AtScale(new Point(), 1);
        for (var delta = 1; delta < 6; delta++)
            Assert.Equal(96, drag.Calculate(new Point(100 + delta, 100), grid, new Size(240, 120), false).Left);
        Assert.Equal(108, drag.Calculate(new Point(107, 100), grid, new Size(240, 120), false).Left);
        Assert.Equal(120, drag.Calculate(new Point(119, 100), grid, new Size(240, 120), false).Left);
    }

    [Theory]
    [InlineData("W")]
    [InlineData("E")]
    [InlineData("N")]
    [InlineData("S")]
    [InlineData("NW")]
    [InlineData("NE")]
    [InlineData("SW")]
    [InlineData("SE")]
    public void DraggedEdgesSnapWhileOppositeEdgesStayFixed(string edge)
    {
        var original = new Rect(101, 103, 400, 250);
        var grid = ScreenGrid.AtScale(new Point(), 1);
        var drag = new PenGeometryDrag(original, new Point(200, 200), edge);
        var result = drag.Calculate(new Point(217, 219), grid, new Size(240, 120), false);
        if (edge.Contains('W')) { Assert.Equal(grid.X(result.Left), result.Left); Assert.Equal(original.Right, result.Right); }
        else Assert.Equal(original.Left, result.Left);
        if (edge.Contains('E')) Assert.Equal(grid.X(result.Right), result.Right);
        else if (!edge.Contains('W')) Assert.Equal(original.Right, result.Right);
        if (edge.Contains('N')) { Assert.Equal(grid.Y(result.Top), result.Top); Assert.Equal(original.Bottom, result.Bottom); }
        else Assert.Equal(original.Top, result.Top);
        if (edge.Contains('S')) Assert.Equal(grid.Y(result.Bottom), result.Bottom);
        else if (!edge.Contains('N')) Assert.Equal(original.Bottom, result.Bottom);
    }

    [Theory]
    [InlineData("NW", 10000, 10000)]
    [InlineData("SE", -10000, -10000)]
    public void ResizeCannotCrossTheOppositeEdgeOrMinimumSize(string edge, double x, double y)
    {
        var original = new Rect(101, 103, 400, 250);
        var grid = ScreenGrid.AtScale(new Point(), 1);
        var result = new PenGeometryDrag(original, new Point(), edge).Calculate(new Point(x, y), grid, new Size(240, 120), false);
        Assert.True(result.Width >= 240);
        Assert.True(result.Height >= 120);
        if (edge == "NW") { Assert.Equal(original.Right, result.Right); Assert.Equal(original.Bottom, result.Bottom); }
        else { Assert.Equal(original.Left, result.Left); Assert.Equal(original.Top, result.Top); }
    }

    [Fact]
    public void NegativeMonitorOriginAndSmoothBypassUseTheSameGestureAnchor()
    {
        var grid = ScreenGrid.AtScale(new Point(-1920, -1080), 1.5);
        var drag = new PenGeometryDrag(new Rect(-1901, -1061, 600, 300), new Point(-1891, -1051), null, 1.5);
        var pointer = new Point(-1850, -1000);
        var snapped = drag.Calculate(pointer, grid, new Size(360, 180), false, 1.5);
        Assert.Equal(grid.X(snapped.Left), snapped.Left);
        Assert.Equal(grid.Y(snapped.Top), snapped.Top);
        var smooth = drag.Calculate(pointer, grid, new Size(360, 180), true, 1.5);
        Assert.Equal(new Point(-1860, -1010), smooth.Location);
        Assert.Equal(snapped, drag.Calculate(pointer, grid, new Size(360, 180), false, 1.5));
    }

    [Fact]
    public void MovingAcrossScalingKeepsLogicalSizeAndLogicalGrabOffset()
    {
        var drag = new PenGeometryDrag(new Rect(-800, 100, 600, 300), new Point(-750, 120), null, 1);
        var result = drag.Calculate(new Point(300, 200), ScreenGrid.AtScale(new Point(), 2),
            new Size(480, 240), true, 2);
        Assert.Equal(new Rect(200, 160, 1200, 600), result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void AutomaticLayoutUsesTheIdenticalMonitorAnchoredGrid(double scale)
    {
        var origin = new Point(-1920, -100);
        var area = new Rect(-1877, -61, 2400 * scale, 1400 * scale);
        var plan = PenLayoutPlanner.Plan(area, scale, PenLayoutMetrics.CommonWidth(17),
            Enumerable.Range(0, 4).Select(index => new PenLayoutItem(index.ToString(), new Rect(0, index, 400, 200), 19, 61)), origin);
        Assert.Null(plan.Error);
        var grid = ScreenGrid.AtScale(origin, scale);
        foreach (var rect in plan.Bounds.Values)
        {
            Assert.Equal(grid.X(rect.Left), rect.Left);
            Assert.Equal(grid.X(rect.Right), rect.Right);
            Assert.Equal(grid.Y(rect.Top), rect.Top);
            Assert.Equal(grid.Y(rect.Bottom), rect.Bottom);
            Assert.True(area.Contains(rect));
            Assert.True(rect.Width / scale >= 600);
            Assert.True(rect.Height / scale >= 3 * PenLayoutMetrics.CellHeight + 61);
        }
    }
}
