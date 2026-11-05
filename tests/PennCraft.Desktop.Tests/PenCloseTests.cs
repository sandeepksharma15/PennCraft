using System.IO;
using System.Windows.Threading;
using Xunit;

namespace PennCraft.Desktop.Tests;

[CollectionDefinition("Pen close UI", DisableParallelization = true)]
public sealed class PenCloseCollection { }

[Collection("Pen close UI")]
public sealed class PenCloseTests
{
    [Fact]
    public async Task EmptyWindowCloseDoesNotRecreateDeletedSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-close", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "pen.json");
        var store = new PenSettingsStore(file);
        Assert.True(store.Save(new PenSettings()));
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
                    window = new MainWindow(store, Guid.NewGuid().ToString("N"));
                    window.Show();
                    await window.ItemsReady;
                    var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.Closed += (_, _) => closed.TrySetResult();
                    window.DeleteEmptyRequested = pen =>
                    {
                        File.Delete(file);
                        pen.CompleteDeletion();
                    };
                    window.Close(); // Exercise the deferred native Closing path as well as Closed.
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                    await Task.Delay(500, token);
                    window.ShowPen(); // Stale tray actions must be harmless after deletion.
                    window.HidePen();
                    Assert.False(window.IsVisible);
                    Assert.False(File.Exists(file));
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally
                {
                    if (window is { IsVisible: true }) window.ExitApplication();
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), token); }
        finally { if (thread.Join(TimeSpan.FromSeconds(5))) Directory.Delete(root, recursive: true); }
    }
}
