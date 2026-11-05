using System.Windows.Interop;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public class DesktopSurfaceTests
{
    [Fact]
    public void NativeSurfaceRemainsIndependentNonActivatingAndNonTopmost()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var source = new HwndSource(new HwndSourceParameters("PennCraft CI test")
                {
                    Width = 240,
                    Height = 160,
                    WindowStyle = unchecked((int)0x80000000)
                });
                var foreground = Native.GetForegroundWindow();
                using var surface = new DesktopSurface(source.Handle);
                surface.Refresh();
                Assert.Equal(nint.Zero, Native.GetParent(source.Handle));
                Assert.Equal((nint)3, Native.SendMessage(source.Handle, 0x0021, 0, 0));
                var style = (long)Native.GetWindowLongPtr(source.Handle, -20);
                Assert.Equal(0x08000080L, style & 0x08000080L);
                Assert.Equal(0L, style & 0x00040008L); // no APPWINDOW or TOPMOST
                Assert.Equal(foreground, Native.GetForegroundWindow());
                surface.Refresh(userVisible: false);
                Assert.False(Native.IsWindowVisible(source.Handle));
                surface.SetEditing(true);
                Assert.Equal(0L, (long)Native.GetWindowLongPtr(source.Handle, -20) & 0x08000000L);
                surface.SetEditing(false);
                Assert.Equal((nint)3, Native.SendMessage(source.Handle, 0x0021, 0, 0));
                if (Native.GetShellWindow() != 0)
                {
                    surface.Refresh();
                    Assert.True(Native.IsWindowVisible(source.Handle));
                    Native.ShowWindow(source.Handle, 0);
                    surface.Refresh();
                    Assert.True(Native.IsWindowVisible(source.Handle));
                    surface.Refresh(userVisible: false);
                    surface.Refresh(userVisible: false);
                    Assert.False(Native.IsWindowVisible(source.Handle));
                }
            }
            catch (Exception exception) { failure = exception; }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Native test did not finish.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
