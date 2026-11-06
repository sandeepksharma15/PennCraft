using System.Windows.Media;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    internal double ItemScale => settings.ItemScale;
    private bool CanChangePresentation => !deleted && CanExit;

    private void ApplyItemScale() => ItemGrid.LayoutTransform = new ScaleTransform(settings.ItemScale, settings.ItemScale);

    internal bool SetItemScale(double value)
    {
        if (!PenItemScale.Values.Contains(value)) return false;
        return ChangePresentation(previous => previous with { ItemScale = value }, ApplyItemScale);
    }

    private bool SetBackgroundOpacity(double value) =>
        ChangePresentation(previous => previous with { BackgroundOpacity = value }, ApplyBackground);

    private bool ChangePresentation(Func<PenSettings, PenSettings> update, Action apply)
    {
        if (!CanChangePresentation) return false;
        FinishEditing(commit: true);
        saveTimer.Stop();
        var previous = TransferSnapshot();
        settings = update(previous).Normalize();
        apply();
        if (SaveSettings()) return true;
        settings = previous;
        apply();
        ItemStatus.Text = "This appearance change could not be saved. Check settings access and try again.";
        return false;
    }
}
