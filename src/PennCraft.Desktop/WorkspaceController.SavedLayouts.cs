using System.Windows;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

internal sealed partial class WorkspaceController
{
    private readonly SavedLayoutStore savedLayouts;
    private readonly SavedLayoutTransaction savedLayoutTransaction;
    private Dictionary<string, LayoutPresentation>? undoSavedLayout;
    private string? undoSavedDisplayStamp;
    private Window? LayoutDialogOwner => pens.FirstOrDefault(pen => pen.IsVisible);

    private bool ReadyForSavedLayout() => !disposed && !WorkspaceBusy
        && savedLayoutTransaction.BlockedReason is null && pens.All(pen => pen.CanArrange);

    private void SaveCurrentLayout()
    {
        if (!ReadyForSavedLayout() || pens.Count == 0)
        {
            MessageBox.Show("Show or create a container and finish current operations before saving a layout.", "PennCraft");
            return;
        }
        layoutBusy = true;
        try
        {
            DismissPreview();
            var name = new LayoutNameDialog("Save current layout", string.Empty, LayoutDialogOwner);
            if (name.ShowDialog() != true) return;
            foreach (var pen in pens) pen.BeginLayout();
            try
            {
                var stamp = DesktopGeometry.DisplayLayoutStamp();
                var captured = pens.ToDictionary(pen => pen.PenId, pen => pen.CapturePresentation());
                if (DesktopGeometry.DisplayLayoutStamp() != stamp) throw new InvalidOperationException("The monitor setup changed while saving.");
                savedLayouts.Add(name.LayoutName, stamp, captured);
            }
            finally { foreach (var pen in pens) pen.EndLayout(); }
        }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception))
        {
            MessageBox.Show("The layout could not be saved. " + exception.Message, "PennCraft");
        }
        finally { layoutBusy = false; }
    }

    private void ManageSavedLayouts()
    {
        if (!ReadyForSavedLayout()) { MessageBox.Show("Finish current operations before opening saved layouts.", "PennCraft"); return; }
        string? selected = null;
        layoutBusy = true;
        try
        {
            DismissPreview();
            var dialog = new SavedLayoutsDialog(savedLayouts, LayoutDialogOwner);
            if (dialog.ShowDialog() == true) selected = dialog.SelectedId;
        }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception))
        {
            MessageBox.Show("Saved layouts could not be opened. The catalog was retained. " + exception.Message, "PennCraft");
        }
        finally { layoutBusy = false; }
        if (selected is not null) RestoreSavedLayout(selected);
    }

    private void RestoreSavedLayout(string id)
    {
        if (!ReadyForSavedLayout()) return;
        layoutBusy = true;
        try
        {
            var layout = savedLayouts.Load().SingleOrDefault(layout => layout.Id == id)
                ?? throw new InvalidOperationException("The saved layout no longer exists.");
            var stamp = DesktopGeometry.DisplayLayoutStamp();
            var areas = System.Windows.Forms.Screen.AllScreens.Select(screen => new Rect(
                screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height)).ToArray();
            var plan = SavedLayoutPlanner.Plan(layout, stamp, pens.Select(pen => pen.PenId), areas);
            if (plan.Error is { } error) { MessageBox.Show(error, "PennCraft"); return; }
            var before = ApplySavedLayout(plan.Pens, stamp);
            if (before is null) return;
            undoSavedLayout = before;
            undoSavedDisplayStamp = stamp;
            undoLayout = null;
            if (plan.MissingCount > 0)
                MessageBox.Show($"Restored the existing containers. Skipped {plan.MissingCount} deleted container(s).", "PennCraft");
        }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception))
        {
            MessageBox.Show("The layout could not be restored. " + exception.Message, "PennCraft");
        }
        finally { layoutBusy = false; }
    }

    private bool CanUndoSavedLayout() => ReadyForSavedLayout() && undoSavedLayout is { Count: > 0 }
        && undoSavedLayout.Keys.All(id => pens.Any(pen => pen.PenId == id));

    private void UndoSavedLayout()
    {
        if (!CanUndoSavedLayout()) return;
        layoutBusy = true;
        try
        {
            if (DesktopGeometry.DisplayLayoutStamp() != undoSavedDisplayStamp)
            {
                MessageBox.Show("The monitor setup changed. The previous presentation was left unchanged.", "PennCraft");
                return;
            }
            if (ApplySavedLayout(undoSavedLayout!, undoSavedDisplayStamp!) is not null) undoSavedLayout = null;
        }
        catch (Exception exception) when (SavedLayoutTransaction.Expected(exception))
        {
            MessageBox.Show("The previous layout could not be restored. " + exception.Message, "PennCraft");
        }
        finally { layoutBusy = false; }
    }

    private Dictionary<string, LayoutPresentation>? ApplySavedLayout(Dictionary<string, LayoutPresentation> target, string displayStamp)
    {
        if (DesktopGeometry.DisplayLayoutStamp() != displayStamp) throw new InvalidOperationException("The monitor setup changed.");
        var selected = pens.Where(pen => target.ContainsKey(pen.PenId)).ToArray();
        Dictionary<string, LayoutPresentation>? before = null;
        var committed = false;
        foreach (var pen in pens) pen.BeginLayout();
        try
        {
            before = selected.ToDictionary(pen => pen.PenId, pen => pen.CapturePresentation());
            committed = savedLayoutTransaction.Execute(before, () =>
            {
                foreach (var pen in selected) pen.ApplyPresentation(target[pen.PenId]);
                if (DesktopGeometry.DisplayLayoutStamp() != displayStamp) throw new InvalidOperationException("The monitor setup changed during restoration.");
                return selected.ToDictionary(pen => pen.PenId, pen => pen.CapturePresentation());
            }, () =>
            {
                foreach (var pen in selected) pen.ApplyPresentation(before[pen.PenId]);
            });
        }
        finally { foreach (var pen in pens) pen.EndLayout(committed); }
        if (savedLayoutTransaction.BlockedReason is { } blocked)
            foreach (var pen in pens) pen.BlockTransferRecovery(blocked);
        if (!committed)
            MessageBox.Show(savedLayoutTransaction.BlockedReason
                ?? "The layout could not be saved. The previous presentation was retained.", "PennCraft");
        return committed ? before : null;
    }
}
