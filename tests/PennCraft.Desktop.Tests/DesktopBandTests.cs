using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class DesktopBandTests
{
    [Fact]
    public void StableOverlappingPensNeedNoRepeatedPlacement()
    {
        nint[] pens = [3, 2, 1];
        DesktopBandWindow[] windows = [new(10, false), new(3, false), new(2, false), new(1, false), new(20, true)];
        for (var tick = 0; tick < 100; tick++) Assert.False(DesktopBandPolicy.NeedsRepair(pens, windows));
    }

    [Fact]
    public void WrongPenOrderOrApplicationBelowPensRequiresRepair()
    {
        Assert.True(DesktopBandPolicy.NeedsRepair([2, 1], [new(1, false), new(2, false)]));
        Assert.True(DesktopBandPolicy.NeedsRepair([2, 1], [new(2, false), new(10, false), new(1, false)]));
        Assert.True(DesktopBandPolicy.NeedsRepair([2, 1], [new(2, false), new(1, false), new(10, false)]));
        Assert.True(DesktopBandPolicy.NeedsRepair([2, 1], [new(1, false)]));
        Assert.False(DesktopBandPolicy.NeedsRepair([2, 1], [new(10, false), new(20, true), new(2, false), new(1, false)]));
    }
}
