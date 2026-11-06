using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

[Collection("Pen close UI")]
public sealed class PenLayoutWindowTests
{
    [Fact]
    public async Task ActualGridFitsSixColumnsAndThreeRowsAndBoundsCanBeRestored()
    {
        var root = Path.Combine(Path.GetTempPath(), "PennCraft-layout-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "pen.json");
        var paths = Enumerable.Range(0, 18).Select(index => Path.Combine(root, $"Item{index}.txt")).ToArray();
        foreach (var path in paths) File.WriteAllText(path, "keep");
        Assert.True(new PenSettingsStore(file).Save(new PenSettings { ItemPaths = paths, Width = 340, Height = 200 }));
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
                    window = new MainWindow(new PenSettingsStore(file), Guid.NewGuid().ToString("N"));
                    window.Show();
                    await window.ItemsReady;
                    window.UpdateLayout();
                    var oldBounds = DesktopGeometry.Bounds(window);
                    var screen = DesktopGeometry.PrimaryLayoutScreen();
                    var width = PenLayoutMetrics.CommonWidth(SystemParameters.VerticalScrollBarWidth);
                    var plan = PenLayoutPlanner.Plan(screen.WorkArea, screen.Scale, width, [window.MeasureLayoutItem(width)]);
                    Assert.Null(plan.Error);
                    window.BeginLayout();
                    try { window.ApplyLayoutBounds(plan.Bounds[window.PenId]); }
                    finally { window.EndLayout(); }
                    window.UpdateLayout();
                    var grid = (ItemsControl)window.FindName("ItemGrid");
                    var scroll = (ScrollViewer)window.FindName("ItemScrollViewer");
                    Point At(int index) => ((FrameworkElement)grid.ItemContainerGenerator.ContainerFromIndex(index))
                        .TransformToAncestor(grid).Transform(new Point());
                    Assert.Equal(At(0).Y, At(5).Y);
                    Assert.True(At(6).Y > At(5).Y);
                    Assert.Equal(At(12).Y, At(17).Y);
                    Assert.True(scroll.ViewportHeight >= PenLayoutMetrics.MinimumRows * PenLayoutMetrics.CellHeight);
                    window.BeginLayout();
                    try { window.ApplyLayoutBounds(oldBounds); }
                    finally { window.EndLayout(); }
                    Assert.Equal(oldBounds, DesktopGeometry.Bounds(window));
                    Assert.All(paths, path => Assert.Equal("keep", File.ReadAllText(path)));
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
