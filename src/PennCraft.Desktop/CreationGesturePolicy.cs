using System.Windows;

namespace PennCraft.Desktop;

internal static class CreationGesturePolicy
{
    public static Rect Rectangle(Point start, Point end) => new(start, end);

    public static bool CanCreate(Rect rectangle, Point start, double scale, bool iconsKnown, bool selected,
        IEnumerable<Rect> icons, IEnumerable<Rect> obstacles)
    {
        if (!iconsKnown || selected || !double.IsFinite(scale) || scale <= 0
            || rectangle.Width < PenSettings.MinimumWidth * scale
            || rectangle.Height < PenSettings.MinimumHeight * scale) return false;
        return !icons.Any(icon => icon.Contains(start) || rectangle.IntersectsWith(icon))
            && !obstacles.Any(rectangle.IntersectsWith);
    }
}
