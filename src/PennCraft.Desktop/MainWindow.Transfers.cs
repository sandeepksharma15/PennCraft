using System.Windows;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal PenTransferCoordinator? Transfers { get; set; }
    internal string PenId => penId;
    internal PenMoveService? MoveService => moves;
    private bool transferBusy;
    private bool transferRecoveryBlocked;

    internal bool CanBeginTransfer(bool source) => geometryGesture is null && !transferBusy && !transferRecoveryBlocked
        && !moveSettingsBlocked && !itemMovesBusy && !itemMenuBusy && !itemReconciliationBusy
        && (source || !itemDragActive) && (IsPortal || moves is { HasUnresolvedMoves: false });

    internal PenSettings TransferSnapshot() => settings with { Left = Left, Top = Top, Width = Width, Height = Height };

    internal void ReserveTransfer()
    {
        transferBusy = true;
        saveTimer.Stop();
        itemRefreshTimer.Stop();
        pressedItem = null;
        itemDragOrigin = null;
        ItemStatus.Text = "Transferring item between Pens…";
    }

    internal void CompleteTransfer(PenSettings? snapshot)
    {
        transferBusy = false;
        if (snapshot is not null)
        {
            // Apply membership only; keep live geometry, title and visibility edits.
            settings = settings with { ItemPaths = snapshot.ItemPaths, TrackedItems = snapshot.TrackedItems };
            if (!settings.ItemPaths.Contains(selectedItemPath ?? string.Empty, StringComparer.OrdinalIgnoreCase)) SelectItem(null);
            RefreshItemViews();
            RestartItemWatchers();
        }
        if (!SaveSettings()) PauseMoves();
    }

    internal void ReleaseTransfer()
    {
        if (!transferBusy) return;
        transferBusy = false;
        if (!SaveSettings()) PauseMoves();
    }

    internal void BlockTransferRecovery(string message)
    {
        transferRecoveryBlocked = true;
        ItemStatus.Text = message;
    }

    internal void SetTransferStatus(string message)
    {
        if (!moveSettingsBlocked) ItemStatus.Text = message;
    }

    private bool PreviewInternalTransfer(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(PenDragRegistry.Format, autoConvert: false)) return false;
        var session = Transfers?.Resolve(e.Data);
        e.Effects = session is not null && Transfers!.CanAccept(session, penId) ? DragDropEffects.Move : DragDropEffects.None;
        return true;
    }

    private async Task<bool> DropInternalTransferAsync(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(PenDragRegistry.Format, autoConvert: false)) return false;
        var session = Transfers?.Resolve(e.Data);
        e.Effects = DragDropEffects.None;
        if (session is not null && Transfers!.CanAccept(session, penId))
        {
            e.Effects = DragDropEffects.Move;
            await Transfers.TransferAsync(session, penId);
        }
        return true;
    }
}
