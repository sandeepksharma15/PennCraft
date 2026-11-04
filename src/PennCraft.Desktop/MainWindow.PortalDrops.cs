using System.IO;
using System.Windows;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private static DragDropEffects PortalIncomingEffect(DragEventArgs e)
    {
        // Link acknowledges receipt without asking Explorer to delete the source a second time.
        if ((e.AllowedEffects & DragDropEffects.Link) == 0
            || !e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false)
            || e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] { Length: 1 } paths)
            return DragDropEffects.None;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.Equals(Path.GetDirectoryName(paths[0]), desktop, StringComparison.OrdinalIgnoreCase)
            ? DragDropEffects.Link : DragDropEffects.None;
    }

    private async Task DropIntoPortalAsync(DragEventArgs e)
    {
        if (PortalIncomingEffect(e) == DragDropEffects.None
            || e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] { Length: 1 } paths) return;
        e.Effects = DragDropEffects.Link;
        ItemStatus.Text = "Moving item into the linked folder…";
        if (Transfers is not null) await Transfers.MoveDesktopItemIntoPortalAsync(this, paths[0]);
    }
}
