using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class DesktopPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void VisibilityRequiresShellAndUserIntent(bool shellAvailable, bool userVisible, bool expected)
    {
        Assert.Equal(expected, DesktopPolicy.ShouldShow(shellAvailable, userVisible));
    }
}
