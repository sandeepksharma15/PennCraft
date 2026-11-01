using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public class DesktopPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void VisibilityRequiresLiveShellAndDesktopFocus(bool shell, bool desktop, bool expected) =>
        Assert.Equal(expected, DesktopPolicy.ShouldShow(shell, desktop));

    [Fact]
    public void RestartAndApplicationSwitchFailClosed()
    {
        Assert.True(DesktopPolicy.ShouldShow(true, true));
        Assert.False(DesktopPolicy.ShouldShow(false, true));
        Assert.False(DesktopPolicy.ShouldShow(true, false));
        Assert.True(DesktopPolicy.ShouldShow(true, true));
    }
}
