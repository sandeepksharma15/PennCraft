using System.IO;
using System.Windows;

namespace PennCraft.Desktop;

public partial class App : Application
{
    // Configure the shared process before WPF or the notification-area component creates an HWND.
    static App() => System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

    private FileStream? instanceLock;
    private WorkspaceController? workspace;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft");
            Directory.CreateDirectory(directory);
            instanceLock = new FileStream(Path.Combine(directory, "instance.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("PennCraft could not acquire its application lock. Close any other instance and check access to its settings folder.",
                "PennCraft", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft");
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var report = await Task.Run(() => new FirstRunOrganizer(new WorkspaceStore(directory), desktop).Run());
            workspace = new WorkspaceController(this);
            if (report is { Skipped.Length: > 0 })
                MessageBox.Show("First-run organization finished. Some items could not be moved and were left on the desktop. They will not be retried automatically.",
                    "PennCraft", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show("The saved workspace could not be opened. Existing settings and files were left untouched.",
                "PennCraft", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        workspace?.Dispose();
        instanceLock?.Dispose();
        base.OnExit(e);
    }
}
