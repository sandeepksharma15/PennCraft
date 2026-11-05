using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace PennCraft.Desktop.Windows;

/// <summary>Immutable automation snapshot; obtained exclusively on the MTA worker.</summary>
internal sealed partial record DesktopIconSnapshot(Rect[] Icons, bool HasSelection, long CapturedAt)
{
    public bool IsFresh => Environment.TickCount64 - CapturedAt < 1800;
    public bool SameLayout(DesktopIconSnapshot other) => Icons.SequenceEqual(other.Icons);

    public static DesktopIconSnapshot? Read()
    {
        try
        {
            var list = DesktopGeometry.IconList();
            if (list == 0) return null;
            if (!Native.IsWindowVisible(list)) return new([], false, Environment.TickCount64);
            if (SendMessageTimeout(list, 0x1004, 0, 0, 2, 250, out var count) == 0) return null;
            var element = AutomationElement.FromHandle(list);
            var children = element.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            if ((long)count != children.Count || !element.TryGetCurrentPattern(SelectionPattern.Pattern, out var selection))
                return null;
            List<Rect> icons = [];
            foreach (AutomationElement child in children)
            {
                var bounds = child.Current.BoundingRectangle;
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return null;
                icons.Add(bounds);
            }
            return new([.. icons.OrderBy(rect => rect.Left).ThenBy(rect => rect.Top)],
                ((SelectionPattern)selection).Current.GetSelection().Length != 0, Environment.TickCount64);
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException
            or COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static partial nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam,
        uint flags, uint timeout, out nint result);
}
