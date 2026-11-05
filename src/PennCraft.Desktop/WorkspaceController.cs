using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using PennCraft.Desktop.Items;
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
    private readonly PenTransferCoordinator transfers;
    private readonly DesktopCreationGesture gesture;
    private WorkspaceSettings settings;
    private CreationPreview? preview;
    private bool disposed;
    private readonly RecycleBinVisibility recycleBin;
    private readonly DispatcherTimer iconTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public WorkspaceController(Application app)
    {
        this.app = app;
        store = new WorkspaceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft"));
        settings = store.Load();
        recycleBin = new RecycleBinVisibility(store.RecycleBinStatePath, new DesktopIconSetting());
        SyncRecycleBin(restore: true);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        transfers = new PenTransferCoordinator(store, desktop, () => pens);
        transfers.Recover();
        foreach (var id in settings.PenIds) OpenPen(id);
        iconTimer.Tick += (_, _) => SyncRecycleBin();
        iconTimer.Start();
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
        var pen = new MainWindow(new PenSettingsStore(store.SettingsPath(id)), id) { Transfers = transfers };
        if (transfers.BlockedReason is { } error) pen.BlockTransferRecovery(error);
        pens.Add(pen);
        pen.Show();
        return pen;
    }

    private void OfferCreation(Rect region)
    {
        if (disposed || !settings.CreationEnabled || pens.Any(pen => !pen.CanExit)) return;
        DismissPreview();
        if (DesktopGeometry.Obstacles().Any(region.IntersectsWith)) return;
        var offered = new CreationPreview(region, bounds => CreatePen(bounds), CreatePortal);
        preview = offered;
        offered.Closed += (_, _) => { if (ReferenceEquals(preview, offered)) preview = null; };
        offered.Show();
    }

    private void DismissPreview() => preview?.Close();

    private void CreatePortal(Rect region)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder for the Portal", Multiselect = false };
        if (picker.ShowDialog() == true) CreatePen(region, picker.FolderName);
    }

    private void CreatePen(Rect? region, string? portalFolder = null)
    {
        if (disposed) return;
        var id = Guid.NewGuid().ToString("N");
        var initial = new PenSettings { Title = portalFolder is null ? "New Pen" : new DirectoryInfo(portalFolder).Name, PortalFolder = portalFolder };
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

    private void SyncRecycleBin(bool restore = false)
    {
        try
        {
            if (restore) recycleBin.Restore();
            else if (pens.All(pen => pen.CanExit))
            {
                // Read durable membership: failed UI saves must never hide the Windows icon.
                var present = settings.PenIds.Any(id => new PenSettingsStore(store.SettingsPath(id)).Load().ItemPaths
                    .Any(VirtualDesktopItem.IsRecycleBin));
                recycleBin.Update(present);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or System.Security.SecurityException)
        {
            Diagnostics.Write($"Recycle Bin visibility could not be updated: {exception.GetType().Name}");
            foreach (var pen in pens) pen.SetTransferStatus("Recycle Bin desktop visibility could not be updated. Check Windows Desktop Icon Settings.");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        iconTimer.Stop();
        SyncRecycleBin(restore: true);
        DismissPreview();
        gesture.Dispose();
        tray.Dispose();
    }
}
