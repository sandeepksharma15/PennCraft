using System.Windows;
using System.Windows.Input;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

internal enum PenDragResult { Cancelled, Desktop, Pen }

/// <summary>Private-token Pen drops plus desktop release; never an Explorer file payload.</summary>
internal static class DesktopItemDrag
{
    internal static DataObject CreateData(PenDragSession session)
    {
        var data = new DataObject();
        data.SetData(PenDragRegistry.Format, session.Token, autoConvert: false);
        return data;
    }

    public static PenDragResult Run(UIElement source, PenDragSession session, Func<bool> canDropOnPen)
    {
        var desktop = false;
        void Continue(object sender, QueryContinueDragEventArgs e)
        {
            var leftDown = (e.KeyStates & DragDropKeyStates.LeftMouseButton) != 0;
            desktop = ItemDragPolicy.ShouldRemove(e.EscapePressed, leftDown, DesktopDropTarget.IsUnderCursor());
            if (e.EscapePressed || desktop)
            {
                e.Action = DragAction.Cancel;
                e.Handled = true;
            }
            else if (!leftDown)
            {
                e.Action = DragAction.Drop;
                e.Handled = true;
            }
        }
        void Feedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            Mouse.SetCursor(DesktopDropTarget.IsUnderCursor() || canDropOnPen() ? Cursors.Arrow : Cursors.No);
            e.Handled = true;
        }

        source.QueryContinueDrag += Continue;
        source.GiveFeedback += Feedback;
        try
        {
            DragDrop.DoDragDrop(source, CreateData(session), DragDropEffects.Move | DragDropEffects.Link);
            return session.Accepted ? PenDragResult.Pen : desktop ? PenDragResult.Desktop : PenDragResult.Cancelled;
        }
        finally
        {
            source.QueryContinueDrag -= Continue;
            source.GiveFeedback -= Feedback;
            Mouse.SetCursor(null);
        }
    }
}
