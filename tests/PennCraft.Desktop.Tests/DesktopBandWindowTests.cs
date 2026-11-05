using System.Windows;
using System.Windows.Interop;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

[Collection("Pen close UI")]
public sealed class DesktopBandWindowTests
{
    [Fact]
    public async Task RepeatedNativeRefreshKeepsOverlappingWindowOrderStable()
    {
        await ShellFileMove.OnSta(() =>
        {
            var first = new Window { Width = 300, Height = 180, Left = 20, Top = 20, ShowActivated = false, ShowInTaskbar = false };
            var second = new Window { Width = 300, Height = 180, Left = 40, Top = 40, ShowActivated = false, ShowInTaskbar = false };
            DesktopSurface? firstSurface = null;
            DesktopSurface? secondSurface = null;
            try
            {
                first.Show();
                var firstHandle = new WindowInteropHelper(first).Handle;
                firstSurface = new DesktopSurface(firstHandle);
                second.Show();
                var secondHandle = new WindowInteropHelper(second).Handle;
                secondSurface = new DesktopSurface(secondHandle);
                for (var tick = 0; tick < 20; tick++)
                {
                    firstSurface.Refresh();
                    secondSurface.Refresh();
                    var order = new List<nint>();
                    var visited = new HashSet<nint>();
                    for (var window = Native.GetTopWindow(0); window != 0 && visited.Add(window); window = Native.GetWindow(window, 2))
                        if (window == firstHandle || window == secondHandle) order.Add(window);
                    Assert.Equal(new[] { secondHandle, firstHandle }, order);
                }
                return true;
            }
            finally
            {
                secondSurface?.Dispose();
                firstSurface?.Dispose();
                second.Close();
                first.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }
}
