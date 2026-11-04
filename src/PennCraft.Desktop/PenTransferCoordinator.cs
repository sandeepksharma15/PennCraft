using System.Windows;
using System.Windows.Interop;
using PennCraft.Desktop.Items;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

/// <summary>Reserves both live windows while a worker commits one workspace-level transfer.</summary>
internal sealed class PenTransferCoordinator(WorkspaceStore store, string desktop, Func<IEnumerable<MainWindow>> windows)
{
    private readonly PenTransferService service = new(store, desktop);
    private readonly PenDragRegistry drags = new();
    private bool busy;

    public string? BlockedReason => service.BlockedReason;
    public string? Recover() => service.Recover();
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
            && source.TransferSnapshot().ItemPaths.Contains(session.Path, StringComparer.OrdinalIgnoreCase);
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
}
