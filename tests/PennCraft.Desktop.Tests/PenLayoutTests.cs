using System.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class PenLayoutTests
{
    [Fact]
    public void PacksTopToBottomThenLeftWithEqualWidthsAndMargins()
    {
        var items = Enumerable.Range(0, 4).Select(index => new PenLayoutItem(index.ToString(),
            new Rect(100, index * 10, 300, 200), 1, 60));
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 1080), 1, 600, items);
        Assert.Null(plan.Error);
        Assert.Equal(new Rect(1296, 24, 600, 330), plan.Bounds["0"]);
        Assert.Equal(new Rect(1296, 378, 600, 330), plan.Bounds["1"]);
        Assert.Equal(new Rect(672, 24, 600, 330), plan.Bounds["2"]);
        Assert.Equal(new Rect(672, 378, 600, 330), plan.Bounds["3"]);
    }

    [Theory]
    [InlineData(0, 330)]
    [InlineData(18, 330)]
    [InlineData(19, 420)]
    [InlineData(24, 420)]
    [InlineData(1000, 1032)]
    public void HeightUsesContentWithThreeRowMinimumAndScreenCap(int count, double expected)
    {
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 1080), 1, 600,
            [new("Pen", new Rect(0, 0, 240, 120), count, 60)]);
        Assert.Null(plan.Error);
        Assert.Equal(expected, plan.Bounds["Pen"].Height);
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void ScalingAndOffsetWorkAreaPreserveSixColumnsAndTaskbarMargins(double scale)
    {
        var area = new Rect(80, 40, 2400 * scale, 1400 * scale);
        var plan = PenLayoutPlanner.Plan(area, scale, PenLayoutMetrics.CommonWidth(40),
            [new("Pen", new Rect(-1000, 100, 300, 200), 18, 60)]);
        Assert.Null(plan.Error);
        var bounds = plan.Bounds["Pen"];
        Assert.Equal(area.Right - Math.Ceiling(24 * scale), bounds.Right);
        Assert.Equal(area.Top + Math.Ceiling(24 * scale), bounds.Top);
        Assert.True(bounds.Width / scale - 20 - 40 >= 6 * PenLayoutMetrics.CellWidth);
        Assert.True(bounds.Height / scale - 60 >= 3 * PenLayoutMetrics.CellHeight);
    }

    [Fact]
    public void RepeatingLayoutDoesNotReshuffleUnchangedColumns()
    {
        var area = new Rect(0, 0, 1920, 1080);
        var items = Enumerable.Range(0, 4).Select(index => new PenLayoutItem(index.ToString(),
            new Rect(100, index * 10, 300, 200), 1, 60)).ToArray();
        var first = PenLayoutPlanner.Plan(area, 1, 600, items);
        var second = PenLayoutPlanner.Plan(area, 1, 600, items.Select(item => item with { CurrentBounds = first.Bounds[item.Id] }));
        Assert.Null(second.Error);
        foreach (var (id, bounds) in first.Bounds) Assert.Equal(bounds, second.Bounds[id]);
    }

    [Fact]
    public void CapacityFailureReturnsNoPartialPlacement()
    {
        var items = Enumerable.Range(0, 10).Select(index => new PenLayoutItem(index.ToString(),
            new Rect(0, index, 300, 200), 18, 60));
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1200, 800), 1, 600, items);
        Assert.NotNull(plan.Error);
        Assert.Empty(plan.Bounds);
        var shortScreen = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 300), 1, 600,
            [new("Pen", new Rect(0, 0, 300, 200), 0, 60)]);
        Assert.NotNull(shortScreen.Error);
        Assert.Empty(shortScreen.Bounds);
    }

    [Fact]
    public void OrderingReadsColumnsRightToLeftThenTopToBottomAndRejectsInvalidInput()
    {
        var plan = PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 1080), 1, 600,
            [new("left", new Rect(0, 10, 300, 200), 0, 60), new("right", new Rect(500, 10, 300, 200), 0, 60)]);
        Assert.True(plan.Bounds["right"].Top < plan.Bounds["left"].Top);
        Assert.Empty(PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 1080), 0, 600, []).Bounds);
        Assert.NotNull(PenLayoutPlanner.Plan(new Rect(0, 0, 1920, 1080), 1, 600,
            [new("same", new Rect(0, 0, 1, 1), 0, 60), new("same", new Rect(0, 0, 1, 1), 0, 60)]).Error);
    }
}
