using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DesktopSurface? surface;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            surface = new DesktopSurface(new WindowInteropHelper(this).Handle);
            timer.Tick += Refresh;
            timer.Start();
            Refresh(this, EventArgs.Empty);
        };
        Loaded += (_, _) => { surface?.InvalidateVisibility(); surface?.Refresh(); };
        Closed += (_, _) => { timer.Stop(); surface?.Dispose(); };
    }

    private void Refresh(object? sender, EventArgs e) => surface?.Refresh();
    private void ClosePen(object sender, RoutedEventArgs e) => Close();
    private void MovePen(object sender, DragDeltaEventArgs e)
    {
        Left += e.HorizontalChange;
        Top += e.VerticalChange;
    }
}
