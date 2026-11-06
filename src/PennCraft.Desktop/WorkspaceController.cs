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
    private readonly PenDeletionService deletions;
    private readonly LayoutTransaction layouts;
    private bool layoutBusy;
    private bool WorkspaceBusy => organizationBusy || layoutBusy;
    private Dictionary<string, Rect>? undoLayout;
    private string? undoDisplayStamp;
    private readonly DesktopCreationGesture gesture;
    private WorkspaceSettings settings;
    private CreationPreview? preview;
    private bool disposed;
    private bool organizationBusy;
    private readonly RecycleBinVisibility recycleBin;
    private bool iconScanBusy;
    private readonly DispatcherTimer iconTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public WorkspaceController(Application app)
    {
        this.app = app;
        store = new WorkspaceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft"));
        settings = store.Load();
        recycleBin = new RecycleBinVisibility(store.RecycleBinStatePath, new DesktopIconSetting());
        UpdateRecycleBinVisibility(false);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        deletions = new PenDeletionService(store, desktop);
        deletions.Recover();
        settings = store.Load();
        layouts = new LayoutTransaction(store);
        layouts.Recover();
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
            () => { if (!WorkspaceBusy) CreatePen(null); }, () => settings.CreationEnabled, ToggleCreation, Exit, ArrangePens, UndoLayout, CanUndoLayout);
    }

    private MainWindow OpenPen(string id)
    {
        var pen = new MainWindow(new PenSettingsStore(store.SettingsPath(id)), id) { Transfers = transfers };
        if ((layouts.BlockedReason ?? deletions.BlockedReason ?? transfers.BlockedReason) is { } error) pen.BlockTransferRecovery(error);
        pen.DeleteEmptyRequested = DeleteEmptyPen;
        pen.DeleteRequested = DeletePen;
        pen.ArrangeRequested = ArrangePens;
        pen.UndoLayoutRequested = UndoLayout;
        pen.CanUndoLayout = CanUndoLayout;
        pens.Add(pen);
        pen.SettingsSaved += (_, _) => RearmOrganizationOffer(pen);
        pen.Show();
        return pen;
    }

    private void DeleteEmptyPen(MainWindow pen) => DeleteContainer(pen, portal: false);

    private void DeletePen(MainWindow pen)
    {
        var message = pen.IsPortal
            ? $"Delete this Folder Portal?{Environment.NewLine}{pen.PortalFolder}{Environment.NewLine}{Environment.NewLine}The linked folder and all its contents will remain unchanged."
            : "Delete this empty Pen permanently? Its saved settings and empty storage will be removed.";
        if (MessageBox.Show(message, "Delete container", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            DeleteContainer(pen, portal: pen.IsPortal);
    }

    private void DeleteContainer(MainWindow pen, bool portal)
    {
        if (disposed || WorkspaceBusy || !pens.Contains(pen) || pens.Any(window => !window.CanBeginTransfer(false)))
        {
            pen.SetTransferStatus("Finish the current operation or recovery before deleting an empty Pen.");
            return;
        }
        var result = deletions.Delete(pen.PenId, portal);
        settings = store.Load();
        if (result.Removed)
        {
            RearmOrganizationOffer();
            pens.Remove(pen);
            if (ReferenceEquals(app.MainWindow, pen)) app.MainWindow = pens.FirstOrDefault();
            pen.CompleteDeletion();
        }
        if (deletions.BlockedReason is { } blocked)
            foreach (var window in pens) window.BlockTransferRecovery(blocked);
        if (result.Error is { } error)
        {
            if (!result.Removed) pen.SetTransferStatus(error);
            else MessageBox.Show(error, "PennCraft", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OfferCreation(Rect region)
    {
        if (disposed || organizationBusy || !settings.CreationEnabled || pens.Any(pen => !pen.CanExit)) return;
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
        if (WorkspaceBusy) return;
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder for the Portal", Multiselect = false };
        if (picker.ShowDialog() == true) CreatePen(region, picker.FolderName);
    }

    private MainWindow? CreatePen(Rect? region, string? portalFolder = null, string? title = null)
    {
        if (disposed) return null;
        var id = Guid.NewGuid().ToString("N");
        var initial = new PenSettings { Title = title ?? (portalFolder is null ? "New Pen" : new DirectoryInfo(portalFolder).Name), PortalFolder = portalFolder };
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
            return pen;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("The Pen could not be created or positioned. Check access to the settings folder.",
                "PennCraft", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void ToggleCreation()
    {
        if (organizationBusy) return;
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
        if (WorkspaceBusy || pens.Any(pen => !pen.CanExit))
        {
            MessageBox.Show("Wait for the current item operation to finish before exiting.", "PennCraft");
            return;
        }
        Dispose();
        foreach (var pen in pens) pen.ExitApplication();
        app.Shutdown();
    }

    internal async Task OfferOrganizationAsync()
    {
        await Task.WhenAll(pens.Select(pen => pen.ItemsReady));
        if (disposed || (deletions.BlockedReason ?? transfers.BlockedReason) is not null || pens.Any(pen => !pen.CanBeginTransfer(false))) return;
        if (!OrganizationOfferPolicy.IsEligible(pens.Select(pen => pen.TransferSnapshot())))
        {
            RearmOrganizationOffer();
            return;
        }
        if (settings.OrganizationOfferDismissed) return;
        organizationBusy = true;
        DismissPreview();
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var preview = await Task.Run(() => new FirstRunOrganizer(store, desktop).Preview(includeSharedShortcuts: true));
            // Tray commands may have shown a Pen while the preview was being read.
            if (disposed || !OrganizationOfferPolicy.IsEligible(pens.Select(pen => pen.TransferSnapshot()))) return;
            if (preview.Count == 0) return;
            var message = $"All Pens are empty and hidden. Organize the desktop now?{Environment.NewLine}{Environment.NewLine}"
                + $"{preview.Groups["Documents"].Length} PDF file(s) → Documents{Environment.NewLine}"
                + $"{preview.Groups["Programs"].Length} shortcut(s) → Programs{Environment.NewLine}"
                + $"{preview.Groups["Folders"].Length} folder(s) → Folders{Environment.NewLine}{Environment.NewLine}"
                + "Other desktop items will stay where they are. Choose No to keep the current layout.";
            var sharedCount = preview.Groups["Programs"].Count(path => SharedDesktopShortcut.Is(path));
            if (sharedCount != 0) message += $"{Environment.NewLine}{Environment.NewLine}{sharedCount} shared desktop shortcut(s) are included. Moving these removes them for all Windows users and Windows may request administrator permission.";
            var choice = MessageBox.Show(message, "Organize desktop", MessageBoxButton.YesNo,
                MessageBoxImage.Question, MessageBoxResult.No);
            if (disposed || pens.Any(pen => !pen.CanBeginTransfer(false))
                || !OrganizationOfferPolicy.IsEligible(pens.Select(pen => pen.TransferSnapshot()))) return;
            var next = settings with { OrganizationOfferDismissed = true };
            store.Save(next); // Persist the decision before any moves.
            settings = next;
            if (choice != MessageBoxResult.Yes) return;
            foreach (var (title, paths) in preview.Groups)
            {
                var pen = pens.FirstOrDefault(pen => !pen.IsPortal
                    && string.Equals(pen.PenTitle, title, StringComparison.OrdinalIgnoreCase))
                    ?? CreatePen(null, title: title);
                if (pen is null) return;
                await pen.ItemsReady;
                pen.ShowPen();
                await pen.MoveIncomingItemsAsync(paths, sharedMoveConfirmed: true);
                if (!pen.CanBeginTransfer(false)) return;
            }
        }
        finally
        {
            organizationBusy = false;
            if (!OrganizationOfferPolicy.IsEligible(pens.Select(pen => pen.TransferSnapshot()))) RearmOrganizationOffer();
        }
    }

    internal async Task OfferSharedShortcutsAsync()
    {
        await Task.WhenAll(pens.Select(pen => pen.ItemsReady));
        var paths = await Task.Run(() => SharedDesktopShortcut.Enumerate());
        if (paths.Length == 0 || disposed || pens.Any(pen => !pen.CanBeginTransfer(false))) return;
        var programs = pens.FirstOrDefault(pen => !pen.IsPortal && string.Equals(pen.PenTitle, "Programs", StringComparison.OrdinalIgnoreCase));
        if (programs is null) return;
        organizationBusy = true;
        try { await programs.MoveSharedShortcutsAsync(paths); }
        finally { organizationBusy = false; }
    }

    private void RearmOrganizationOffer(MainWindow? changed = null)
    {
        if (!settings.OrganizationOfferDismissed || WorkspaceBusy) return;
        if (changed is not null && OrganizationOfferPolicy.IsEligible([changed.TransferSnapshot()])) return;
        var next = settings with { OrganizationOfferDismissed = false };
        try { store.Save(next); settings = next; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Write($"Organization offer preference could not be saved: {exception.GetType().Name}");
        }
    }

    private bool CanUndoLayout() => !WorkspaceBusy && undoLayout is { Count: > 0 }
        && undoLayout.Keys.All(id => pens.Any(pen => pen.PenId == id && pen.UserVisible)) && pens.All(pen => pen.CanArrange);

    private async void ArrangePens()
    {
        if (disposed || WorkspaceBusy) return;
        layoutBusy = true;
        try
        {
            await Task.WhenAll(pens.Select(pen => pen.LayoutReady));
            if (disposed) return;
            if (pens.Any(pen => !pen.CanArrange))
            {
                MessageBox.Show("Wait for item operations and folder refreshes to finish before arranging Pens.", "PennCraft");
                return;
            }
            var selected = pens.Where(pen => pen.UserVisible).ToArray();
            if (selected.Length == 0)
            {
                MessageBox.Show("Show at least one Pen or Folder Portal to arrange.", "PennCraft");
                return;
            }
            var screen = DesktopGeometry.PrimaryLayoutScreen();
            var stamp = DesktopGeometry.DisplayLayoutStamp();
            var width = PenLayoutMetrics.CommonWidth(SystemParameters.VerticalScrollBarWidth);
            var plan = PenLayoutPlanner.Plan(screen.WorkArea, screen.Scale, width, selected.Select(pen => pen.MeasureLayoutItem(width)), screen.GridOrigin);
            if (plan.Error is { } error) { MessageBox.Show(error, "PennCraft"); return; }
            var previous = selected.ToDictionary(pen => pen.PenId, DesktopGeometry.Bounds);
            if (CommitLayout(plan.Bounds))
            {
                undoLayout = previous;
                undoDisplayStamp = stamp;
            }
        }
        catch (Exception exception) when (LayoutError(exception))
        {
            MessageBox.Show("Windows could not measure or place the layout. Check the display setup and try again.", "PennCraft");
        }
        finally { layoutBusy = false; }
    }

    private void UndoLayout()
    {
        if (disposed || !CanUndoLayout()) return;
        layoutBusy = true;
        try
        {
            if (DesktopGeometry.DisplayLayoutStamp() != undoDisplayStamp)
            {
                MessageBox.Show("The display setup changed. Arrange Pens again before using Undo.", "PennCraft");
                return;
            }
            if (CommitLayout(undoLayout!)) { undoLayout = null; undoDisplayStamp = null; }
        }
        catch (Exception exception) when (LayoutError(exception))
        {
            MessageBox.Show("The previous layout could not be restored. Check the display setup and try again.", "PennCraft");
        }
        finally { layoutBusy = false; }
    }

    private bool CommitLayout(Dictionary<string, Rect> bounds)
    {
        var all = pens.ToArray();
        var selected = all.Where(pen => bounds.ContainsKey(pen.PenId)).ToArray();
        var previous = selected.ToDictionary(pen => pen.PenId, DesktopGeometry.Bounds);
        foreach (var pen in all) pen.BeginLayout();
        var committed = false;
        try
        {
            var before = selected.ToDictionary(pen => pen.PenId, pen => pen.GeometryPersistenceSnapshot());
            committed = layouts.Execute(before, () =>
            {
                foreach (var pen in selected) pen.ApplyLayoutBounds(bounds[pen.PenId]);
                return selected.ToDictionary(pen => pen.PenId, pen => pen.TransferSnapshot());
            }, () =>
            {
                foreach (var pen in selected) pen.ApplyLayoutBounds(previous[pen.PenId]);
            });
        }
        finally { foreach (var pen in all) pen.EndLayout(committed); }
        if (layouts.BlockedReason is { } blocked)
            foreach (var pen in all) pen.BlockTransferRecovery(blocked);
        if (!committed) MessageBox.Show(layouts.BlockedReason ?? "The layout could not be saved. The previous arrangement was retained.", "PennCraft");
        return committed;
    }

    private static bool LayoutError(Exception exception) => exception is System.ComponentModel.Win32Exception
        or InvalidOperationException or ArgumentException or IOException;

    private async void SyncRecycleBin()
    {
        if (disposed || iconScanBusy || pens.Any(pen => !pen.CanExit)) return;
        iconScanBusy = true;
        try
        {
            var ids = settings.PenIds.ToArray();
            // File reads and JSON parsing must not occupy the WPF dispatcher.
            var present = await Task.Run(() => ids.Any(id =>
            {
                var saved = new PenSettingsStore(store.SettingsPath(id)).Load();
                return saved.PortalFolder is null && saved.ItemPaths.Any(VirtualDesktopItem.IsRecycleBin);
            }));
            if (!disposed && pens.All(pen => pen.CanExit)) UpdateRecycleBinVisibility(present);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or System.Security.SecurityException)
        {
            ReportRecycleBinError(exception);
        }
        finally { iconScanBusy = false; }
    }

    private void UpdateRecycleBinVisibility(bool hide)
    {
        try { recycleBin.Update(hide); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or System.Security.SecurityException)
        {
            ReportRecycleBinError(exception);
        }
    }

    private void ReportRecycleBinError(Exception exception)
    {
        Diagnostics.Write($"Recycle Bin visibility could not be updated: {exception.GetType().Name}");
        foreach (var pen in pens) pen.SetTransferStatus("Recycle Bin desktop visibility could not be updated. Check Windows Desktop Icon Settings.");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        iconTimer.Stop();
        UpdateRecycleBinVisibility(false);
        DismissPreview();
        gesture.Dispose();
        tray.Dispose();
    }
}
