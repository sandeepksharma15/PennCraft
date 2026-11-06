using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

public partial class MainWindow
{
    private PenGeometryDrag? geometryGesture;
    private Thumb? geometryThumb;

    private void GeometryDragStarted(object sender, DragStartedEventArgs e)
    {
        var thumb = (Thumb)sender;
        if (!CanExit || deleted) { thumb.CancelDrag(); return; }
        try
        {
            saveTimer.Stop();
            geometryGesture = new(DesktopGeometry.Bounds(this), DesktopGeometry.CursorPosition(), thumb.Tag as string,
                VisualTreeHelper.GetDpi(this).DpiScaleX);
            geometryThumb = thumb;
        }
        catch (Exception exception) when (GeometryError(exception))
        {
            thumb.CancelDrag();
            ItemStatus.Text = "Windows could not start this move or resize.";
        }
    }

    private void UpdateGeometryDrag()
    {
        if (geometryGesture is not { } gesture) return;
        try
        {
            var pointer = DesktopGeometry.CursorPosition();
            var target = DesktopGeometry.GridAt(pointer);
            var current = VisualTreeHelper.GetDpi(this);
            var minimum = new Size(Math.Ceiling(PenSettings.MinimumWidth * Math.Max(current.DpiScaleX, target.Scale)),
                Math.Ceiling(PenSettings.MinimumHeight * Math.Max(current.DpiScaleY, target.Scale)));
            var bounds = gesture.Calculate(pointer, target.Grid, minimum, (Keyboard.Modifiers & ModifierKeys.Alt) != 0, target.Scale);
            var currentBounds = DesktopGeometry.Bounds(this);
            if (gesture.Edge is null)
            {
                if (currentBounds.Location == bounds.Location) return;
                DesktopGeometry.Move(this, bounds.Location);
                UpdateLayout();
            }
            else if (currentBounds != bounds) ApplyLayoutBounds(bounds);
        }
        catch (Exception exception) when (GeometryError(exception))
        {
            geometryThumb?.CancelDrag();
            ItemStatus.Text = "Windows could not complete this move or resize.";
        }
    }

    private void GeometryDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (geometryGesture is not { } gesture) return;
        if (!e.Canceled)
        {
            UpdateGeometryDrag();
            if (geometryGesture is null) return;
        }
        geometryGesture = null;
        geometryThumb = null;
        try { if (e.Canceled) ApplyLayoutBounds(gesture.Bounds); }
        catch (Exception exception) when (GeometryError(exception))
        {
            ItemStatus.Text = "The previous bounds could not be restored. Use Reset position if necessary.";
        }
        SaveSettings();
    }

    private void GeometryDragKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (geometryThumb is null || e.Key != Key.Escape) return;
        e.Handled = true;
        geometryThumb.CancelDrag();
    }

    private static bool GeometryError(Exception exception) => exception is Win32Exception or InvalidOperationException or ArgumentException;
}
