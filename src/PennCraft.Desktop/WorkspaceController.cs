using System.IO;
using System.Text.Json;
using System.Windows;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

/// <summary>Owns windows, one tray icon, the creation observer and the durable workspace index.</summary>
internal sealed class WorkspaceController : IDisposable
{
    private readonly Application app;
    private readonly WorkspaceStore store;
    private readonly List<MainWindow> pens = [];
    private readonly TrayCommands tray;
    private readonly DesktopCreationGesture gesture;
    private WorkspaceSettings settings;
    private CreationPreview? preview;
    private bool disposed;

    public WorkspaceController(Application app)
    {
        this.app = app;
        store = new WorkspaceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft"));
        settings = store.Load();
        foreach (var id in settings.PenIds) OpenPen(id);
        gesture = new DesktopCreationGesture(app.Dispatcher, OfferCreation, DismissPreview)
        {
            Enabled = settings.CreationEnabled
        };
        tray = new TrayCommands(app.Dispatcher,
            () => pens.Select(pen => new PenTrayEntry(pen.PenTitle, pen.UserVisible, pen.ShowPen, pen.HidePen, pen.ResetPosition)),
            () => pens.ForEach(pen => pen.ShowPen()), () => pens.ForEach(pen => pen.HidePen()),
            () => CreatePen(null), () => settings.CreationEnabled, ToggleCreation, Exit);
    }

    private MainWindow OpenPen(string id)
    {
        var pen = new MainWindow(new PenSettingsStore(store.SettingsPath(id)), id);
        pens.Add(pen);
        pen.Show();
        return pen;
    }

    private void OfferCreation(Rect region)
    {
        if (disposed || !settings.CreationEnabled || pens.Any(pen => !pen.CanExit)) return;
        DismissPreview();
        if (DesktopGeometry.Obstacles().Any(region.IntersectsWith)) return;
        var offered = new CreationPreview(region, bounds => CreatePen(bounds));
        preview = offered;
        offered.Closed += (_, _) => { if (ReferenceEquals(preview, offered)) preview = null; };
        offered.Show();
    }

    private void DismissPreview() => preview?.Close();

    private void CreatePen(Rect? region)
    {
        if (disposed) return;
        var id = Guid.NewGuid().ToString("N");
        var initial = new PenSettings { Title = "New Pen" };
        try
        {
            // New settings and index must both be durable before a persistent window is shown.
            if (!new PenSettingsStore(store.SettingsPath(id)).Save(initial))
                throw new IOException("The new Pen settings could not be saved.");
            var next = settings with { PenIds = [.. settings.PenIds, id] };
            store.Save(next);
            settings = next;
            var pen = OpenPen(id);
            if (region is { } pixels) pen.PlaceAt(pixels);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("The Pen could not be created or positioned. Check access to the settings folder.",
                "PennCraft", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleCreation()
    {
        var next = settings with { CreationEnabled = !settings.CreationEnabled };
        try { store.Save(next); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("The desktop creation preference could not be saved.", "PennCraft");
            return;
        }
        settings = next;
        gesture.Enabled = settings.CreationEnabled;
        DismissPreview();
    }

    private void Exit()
    {
        if (pens.Any(pen => !pen.CanExit))
        {
            MessageBox.Show("Wait for the current item operation to finish before exiting.", "PennCraft");
            return;
        }
        Dispose();
        foreach (var pen in pens) pen.ExitApplication();
        app.Shutdown();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DismissPreview();
        gesture.Dispose();
        tray.Dispose();
    }
}
