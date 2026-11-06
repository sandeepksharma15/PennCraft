using System.Windows;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal Action? SaveLayoutRequested { get; set; }
    internal Action? SavedLayoutsRequested { get; set; }
    internal Action? UndoSavedLayoutRequested { get; set; }
    internal Func<bool>? CanUndoSavedLayout { get; set; }

    internal LayoutPresentation CapturePresentation()
    {
        if (geometryFallback is not null)
            throw new InvalidOperationException("Reset the container position or restart before saving a layout.");
        return LayoutPresentation.Capture(TransferSnapshot(), DesktopGeometry.Bounds(this));
    }

    internal void ApplyPresentation(LayoutPresentation presentation)
    {
        if (!transferBusy) throw new InvalidOperationException("Reserve the window before restoring a layout.");
        presentation.Validate();
        settings = presentation.Merge(settings);
        ApplyFoldedPresentation();
        ApplyItemScale();
        ApplyLayoutBounds(presentation.Bounds.ToRect());
        // ApplyLayoutBounds retains the expanded height while folded.
        settings = settings with { Height = presentation.IsFolded ? presentation.Height : Height };
        if (presentation.IsVisible) Show(); else Hide();
        RefreshSurface();
    }
}
