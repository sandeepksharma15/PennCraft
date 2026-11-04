using System.IO;
using System.Windows;

namespace PennCraft.Desktop;

public partial class App : Application
{
    private FileStream? instanceLock;

    protected override void OnStartup(StartupEventArgs e)
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instanceLock?.Dispose();
        base.OnExit(e);
    }
}
