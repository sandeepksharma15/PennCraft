using System.Windows;
using System.Windows.Interop;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

/// <summary>Reserves both live windows while a worker commits one workspace-level transfer.</summary>
internal sealed class PenTransferCoordinator(WorkspaceStore store, string desktop, Func<IEnumerable<MainWindow>> windows)
{
    private readonly PenTransferService service = new(store, desktop);
    private readonly PortalMoveService portalMoves = new(store, desktop);
    private readonly PenDragRegistry drags = new();
    private bool busy;

    public string? BlockedReason => service.BlockedReason ?? portalMoves.BlockedReason;
    public string? Recover() => service.Recover() ?? portalMoves.Recover();
    public PenDragSession Begin(string id, string path, string? identity) => drags.Begin(id, path, identity);
    public void End(PenDragSession session) => drags.End(session);

    public PenDragSession? Resolve(IDataObject data)
    {
        if (!data.GetDataPresent(PenDragRegistry.Format, autoConvert: false)) return null;
        return drags.Resolve(data.GetData(PenDragRegistry.Format, autoConvert: false) as string);
    }

    public bool CanAccept(PenDragSession session, string targetId)
    {
        var source = windows().FirstOrDefault(window => window.PenId == session.SourcePenId);
        var target = windows().FirstOrDefault(window => window.PenId == targetId);
        return !busy && BlockedReason is null && ReferenceEquals(drags.Resolve(session.Token), session)
            && source is not null && target is not null && !ReferenceEquals(source, target)
            && source.CanBeginTransfer(source: true) && target.CanBeginTransfer(source: false)
            && source.ContainsDisplayedItem(session.Path)
            && (!(source.IsPortal || target.IsPortal) || source.IsPortal || source.MoveService?.IsManaged(session.Path) == true);
    }

    public bool CanDropUnderCursor(PenDragSession session)
    {
        var root = DesktopDropTarget.RootUnderCursor();
        var target = windows().FirstOrDefault(window => new WindowInteropHelper(window).Handle == root);
        return target is not null && CanAccept(session, target.PenId);
    }

    public async Task TransferAsync(PenDragSession session, string targetId)
    {
        if (!CanAccept(session, targetId)) return;
        var source = windows().Single(window => window.PenId == session.SourcePenId);
        var target = windows().Single(window => window.PenId == targetId);
        if (source.IsPortal || target.IsPortal)
        {
            session.Accepted = true;
            await MoveViaPortalAsync(source, target, session.Path, session.Identity);
            return;
        }
        busy = true;
        session.Accepted = true;
        source.ReserveTransfer();
        target.ReserveTransfer();
        try
        {
            var sourceSnapshot = source.TransferSnapshot();
            var targetSnapshot = target.TransferSnapshot();
            var sourceMoves = source.MoveService!;
            var targetMoves = target.MoveService!;
            var result = await Task.Run(() => service.Transfer(source.PenId, target.PenId, session.Path,
                session.Identity, sourceSnapshot, targetSnapshot, sourceMoves, targetMoves));
            source.CompleteTransfer(result.Completed ? result.Source : null);
            target.CompleteTransfer(result.Completed ? result.Target : null);
            if (result.NeedsRecovery)
                foreach (var pen in windows()) pen.BlockTransferRecovery(result.Error!);
            var message = result.Completed ? $"Transferred to {target.PenTitle}." : result.Error!;
            source.SetTransferStatus(message);
            target.SetTransferStatus(result.Completed ? $"Received item from {source.PenTitle}." : message);
        }
        finally
        {
            source.ReleaseTransfer();
            target.ReleaseTransfer();
            busy = false;
        }
    }

    public async Task MovePortalItemToDesktopAsync(PenDragSession session)
    {
        var source = windows().FirstOrDefault(window => window.PenId == session.SourcePenId);
        if (busy || BlockedReason is not null || source is null || !source.IsPortal
            || !ReferenceEquals(drags.Resolve(session.Token), session)
            || !source.CanBeginTransfer(true) || !source.ContainsDisplayedItem(session.Path)) return;
        session.Accepted = true;
        await MoveViaPortalAsync(source, null, session.Path, session.Identity);
    }

    public async Task MoveDesktopItemIntoPortalAsync(MainWindow target, string path)
    {
        if (busy || BlockedReason is not null || !target.IsPortal || !target.CanBeginTransfer(false)) return;
        var identity = await Task.Run(() => ItemTracking.TryIdentity(path));
        if (busy || !target.CanBeginTransfer(false)) return;
        await MoveViaPortalAsync(null, target, path, identity);
    }

    public async Task<bool> MoveSharedShortcutIntoPenAsync(MainWindow target, string path)
    {
        if (busy || BlockedReason is not null || target.IsPortal || !SharedDesktopShortcut.Is(path)
            || !target.CanBeginTransfer(false)) return false;
        var identity = await Task.Run(() => ItemTracking.TryIdentity(path));
        if (busy || !target.CanBeginTransfer(false)) return false;
        return await MoveViaPortalAsync(null, target, path, identity);
    }

    private async Task<bool> MoveViaPortalAsync(MainWindow? source, MainWindow? target, string path, string? identity)
    {
        var all = windows().ToArray();
        // Reserve every container: multiple Portals may display the same underlying directory.
        if (all.Any(window => !window.CanBeginTransfer(ReferenceEquals(window, source)))) return false;
        foreach (var window in all)
            if (!window.FlushBeforePortalMove()) { (target ?? source)!.SetTransferStatus("Settings could not be saved. Move not started."); return false; }
        var folder = target?.PortalFolder ?? target?.MoveService?.StorageDirectory ?? desktop;
        if (target is { IsPortal: false } && target.MoveService!.PrepareTransferStorage(create: true) is { } error)
        {
            target.SetTransferStatus(error);
            return false;
        }
        if (source is { IsPortal: false } && source.MoveService!.PrepareTransferStorage(create: false) is { } sourceError)
        {
            source.SetTransferStatus(sourceError);
            return false;
        }
        busy = true;
        foreach (var window in all) window.ReserveTransfer();
        var owner = new WindowInteropHelper(target ?? source!).Handle;
        try
        {
            var message = await ShellFileMove.OnSta(() => portalMoves.Move(source?.PenId, target?.PenId,
                path, folder, identity, (from, to) => ShellFileMove.Move(from, to, owner)));
            foreach (var window in all) await window.ReloadAfterPortalMoveAsync();
            if (portalMoves.BlockedReason is { } blocked)
                foreach (var window in all) window.BlockTransferRecovery(blocked);
            foreach (var window in all) window.SetTransferStatus(message ?? "Moved item.");
            return message is null;
        }
        finally
        {
            foreach (var window in all) window.ReleaseTransfer();
            busy = false;
        }
    }
}
