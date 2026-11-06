using System.IO;
using System.Windows.Threading;
using Xunit;

namespace PennCraft.Desktop.Tests;

/// <summary>Runs an isolated settings-backed Pen on its own STA dispatcher.</summary>
internal static class PenWindowTestHost
{
    internal static async Task RunAsync(bool portal, Func<MainWindow, PenSettingsStore, string, Task> test, int itemCount = 1, string? penId = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-window-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var paths = Enumerable.Range(0, itemCount).Select(index => Path.Combine(root, index == 0 ? "Item.txt" : $"Entry{index}.txt")).ToArray();
        foreach (var file in paths) File.WriteAllText(file, "keep");
        var store = new PenSettingsStore(Path.Combine(root, "pen.json"));
        Assert.True(store.Save(new PenSettings
        {
            Width = 420, Height = 260,
            PortalFolder = portal ? root : null,
            ItemPaths = portal ? [] : paths
        }));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = TestContext.Current.CancellationToken;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                MainWindow? window = null;
                try
                {
                    window = new MainWindow(store, penId ?? Guid.NewGuid().ToString("N"));
                    window.Show();
                    await test(window, store, root);
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally
                {
                    window?.ExitApplication();
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(40), token); }
        finally { if (thread.Join(TimeSpan.FromSeconds(5))) Directory.Delete(root, recursive: true); }
    }
}
