using System.Windows;
using System.Windows.Input;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

/// <summary>Drag membership to the desktop without exposing file-move data to Explorer.</summary>
internal static class DesktopItemDrag
{
    private const string MembershipFormat = "PennCraft.PenMembership";

    public static bool Run(UIElement source, string path)
    {
        var remove = false;
        void Continue(object sender, QueryContinueDragEventArgs e)
        {
            var leftDown = (e.KeyStates & DragDropKeyStates.LeftMouseButton) != 0;
            remove = ItemDragPolicy.ShouldRemove(e.EscapePressed, leftDown, DesktopDropTarget.IsUnderCursor());
            if (e.EscapePressed || !leftDown)
            {
                // This is a membership edit handled locally, never an Explorer file operation.
                e.Action = DragAction.Cancel;
                e.Handled = true;
            }
        }
        void Feedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            Mouse.SetCursor(DesktopDropTarget.IsUnderCursor() ? Cursors.Arrow : Cursors.No);
            e.Handled = true;
        }

        source.QueryContinueDrag += Continue;
        source.GiveFeedback += Feedback;
        try
        {
            var data = new DataObject();
            data.SetData(MembershipFormat, path, autoConvert: false);
            DragDrop.DoDragDrop(source, data, DragDropEffects.Link);
            return remove;
        }
        finally
        {
            source.QueryContinueDrag -= Continue;
            source.GiveFeedback -= Feedback;
            Mouse.SetCursor(null);
        }
    }
}
