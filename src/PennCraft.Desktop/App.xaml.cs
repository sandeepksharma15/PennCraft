using System.IO;
using System.Windows;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class App : Application
{
    // Configure DPI before either desktop UI stack creates a window.
    static App() => System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

    private FileStream? instanceLock;
    private Mutex? installerMutex;
    private WorkspaceController? workspace;
    private bool reportingFailure;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is ["--smoke-test", var smokeReport])
        {
            Shutdown(await ReleaseSmokeTest.RunAsync(smokeReport));
            return;
        }
        if (e.Args.Length != 0)
        {
            MessageBox.Show("PennCraft does not recognize these command-line arguments.", "PennCraft");
            Shutdown(2);
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportFatal(args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Diagnostics.Write("Fatal process error: " + args.ExceptionObject);
        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            instanceLock = new FileStream(Path.Combine(AppInfo.DataDirectory, "instance.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            installerMutex = new Mutex(false, AppInfo.AppMutexName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("PennCraft could not acquire its application lock. Close any other instance and check access to its settings folder.",
                "PennCraft", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(1);
            return;
        }

        try
        {
            var store = new WorkspaceStore(AppInfo.DataDirectory);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var organize = false;
            if (store.IsUninitialized)
            {
                if (Directory.Exists(Path.Combine(desktop, ".PennCraft")))
                {
                    MessageBox.Show("Existing PennCraft desktop storage was found without its workspace settings. Existing files will be left untouched. Restore your settings backup if you have one.",
                        "PennCraft", MessageBoxButton.OK, MessageBoxImage.Information);
                    FirstRunSetup.StartEmpty(store);
                }
                else
                {
                    var choice = MessageBox.Show(
                        "Welcome to PennCraft. Organize your desktop now?\n\nPDFs → Documents, shortcuts → Programs, folders → Folders.\n\nThis physically moves those items into PennCraft storage on your desktop. Other items stay where they are.\n\nChoose No to start with an empty Pen, or Cancel to exit.",
                        "Welcome to PennCraft", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
                    if (choice == MessageBoxResult.Cancel) { Shutdown(); return; }
                    organize = choice == MessageBoxResult.Yes;
                    if (!organize) FirstRunSetup.StartEmpty(store);
                }
            }
            var report = organize ? await Task.Run(() => new FirstRunOrganizer(store, desktop).Run()) : null;
            workspace = new WorkspaceController(this);
            await workspace.OfferOrganizationAsync();
            if (report is not null) await workspace.OfferSharedShortcutsAsync();
            if (report is { Skipped.Length: > 0 })
                MessageBox.Show("First-run organization finished. Some items could not be moved and were left on the desktop. They will not be retried automatically.",
                    "PennCraft", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ReportFatal(exception);
        }
    }

    private void ReportFatal(Exception exception)
    {
        Diagnostics.Write("Fatal application error: " + exception);
        if (reportingFailure) return;
        reportingFailure = true;
        MessageBox.Show(@"PennCraft encountered an unexpected error and must close. Saved settings and recovery records were retained. Restart to recover interrupted work. Details are in %LOCALAPPDATA%\PennCraft\desktop.log.",
            "PennCraft", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        workspace?.Dispose();
        instanceLock?.Dispose();
        installerMutex?.Dispose();
        base.OnExit(e);
    }
}
