using System.IO;
using System.Windows.Interop;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class ShellContextMenuTests
{
    [Fact]
    public void NativeUnicodeItemMenuCanBeQueriedAndDisposedWithoutInvokingAnyCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-menu-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "दस्तावेज.txt");
        File.WriteAllText(path, "Do not modify");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var source = new HwndSource(new HwndSourceParameters("PennCraft menu CI")
                {
                    Width = 100, Height = 100, WindowStyle = unchecked((int)0x80000000)
                });
                using var menu = new ShellContextMenu(path, source.Handle, managed: false);
                Assert.True(menu.ShellCommandCount > 0);
                var verbs = Enumerable.Range(1, menu.ShellCommandCount).Select(id => menu.CanonicalVerb((uint)id)).ToArray();
                Assert.Contains(verbs, verb => string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase));
                Assert.Throws<ArgumentOutOfRangeException>(() => menu.Invoke(0));
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        try
        {
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Native menu query did not finish.");
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            Assert.Equal("Do not modify", File.ReadAllText(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
