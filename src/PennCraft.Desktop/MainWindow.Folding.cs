using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal const double FoldedHeight = PenSettings.FoldedHeight;
    internal bool IsFolded => settings.IsFolded;

    private void HeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || editing) return;
        // Buttons keep their own double-click behavior; the title and drag surface toggle folding.
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != Header;
             node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase) return;
        e.Handled = true;
        ToggleFold();
    }

    internal bool ToggleFold()
    {
        if (deleted || !CanExit) return false;
        FinishEditing(commit: true);
        saveTimer.Stop();
        var previous = TransferSnapshot();
        settings = previous with { IsFolded = !previous.IsFolded };
        ApplyFoldedPresentation();
        if (SaveSettings()) return true;
        // A failed settings write must not leave a fold state that disappears on restart.
        settings = previous;
        ApplyFoldedPresentation();
        ItemStatus.Text = "The fold state could not be saved. Check settings access and try again.";
        return false;
    }

    private void ApplyFoldedPresentation()
    {
        MinHeight = settings.IsFolded ? FoldedHeight : PenSettings.MinimumHeight;
        Height = settings.IsFolded ? FoldedHeight : settings.Height;
        PenContent.Visibility = settings.IsFolded ? Visibility.Collapsed : Visibility.Visible;
        ResizeGrips.Visibility = settings.IsFolded ? Visibility.Collapsed : Visibility.Visible;
    }
}
