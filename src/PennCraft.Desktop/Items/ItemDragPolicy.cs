using System.Windows;

namespace PennCraft.Desktop.Items;

internal static class ItemDragPolicy
{
    // Never return Move for external drops: Explorer must retain its original item.
    public static DragDropEffects IncomingEffect(DragDropEffects allowed, bool hasFiles) =>
        !hasFiles ? DragDropEffects.None :
        (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link :
        (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;

    public static bool ShouldRemove(bool escaped, bool leftButtonDown, bool desktopUnderCursor) =>
        !escaped && !leftButtonDown && desktopUnderCursor;
}
