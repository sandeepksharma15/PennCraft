using System.Windows;

namespace PennCraft.Desktop;

/// <summary>Integer physical-pixel grid anchored to a monitor, never to a Pen's previous border.</summary>
internal readonly record struct ScreenGrid(Point Origin, double Step)
{
    internal static ScreenGrid AtScale(Point origin, double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y)) throw new ArgumentOutOfRangeException(nameof(origin));
        return new(origin, Math.Max(1, Math.Round(12 * scale, MidpointRounding.AwayFromZero)));
    }

    internal double X(double value) => Origin.X + Math.Round((value - Origin.X) / Step, MidpointRounding.AwayFromZero) * Step;
    internal double Y(double value) => Origin.Y + Math.Round((value - Origin.Y) / Step, MidpointRounding.AwayFromZero) * Step;
    internal double FloorX(double value) => Origin.X + Math.Floor((value - Origin.X) / Step) * Step;
    internal double FloorY(double value) => Origin.Y + Math.Floor((value - Origin.Y) / Step) * Step;
    internal double CeilingX(double value) => Origin.X + Math.Ceiling((value - Origin.X) / Step) * Step;
    internal double CeilingY(double value) => Origin.Y + Math.Ceiling((value - Origin.Y) / Step) * Step;
    internal double LengthUp(double value) => Math.Ceiling(value / Step) * Step;
}

/// <summary>Absolute pointer displacement prevents repeated small deltas from being rounded away.</summary>
internal sealed record PenGeometryDrag(Rect Bounds, Point Pointer, string? Edge, double StartScale = 1)
{
    internal Rect Calculate(Point pointer, ScreenGrid grid, Size minimum, bool smooth, double targetScale = 1)
    {
        var dx = pointer.X - Pointer.X;
        var dy = pointer.Y - Pointer.Y;
        if (Edge is null)
        {
            var ratio = targetScale / StartScale;
            var moveLeft = pointer.X - (Pointer.X - Bounds.Left) * ratio;
            var moveTop = pointer.Y - (Pointer.Y - Bounds.Top) * ratio;
            return new Rect(smooth ? moveLeft : grid.X(moveLeft), smooth ? moveTop : grid.Y(moveTop),
                Bounds.Width * ratio, Bounds.Height * ratio);
        }
        if (Edge is not ("W" or "E" or "N" or "S" or "NW" or "NE" or "SW" or "SE"))
            throw new ArgumentException("Unknown resize edge.", nameof(Edge));
        var left = Bounds.Left;
        var right = Bounds.Right;
        var top = Bounds.Top;
        var bottom = Bounds.Bottom;
        if (Edge.Contains('W'))
        {
            left = smooth ? Bounds.Left + dx : grid.X(Bounds.Left + dx);
            if (left > right - minimum.Width) left = smooth ? right - minimum.Width : grid.FloorX(right - minimum.Width);
        }
        if (Edge.Contains('E'))
        {
            right = smooth ? Bounds.Right + dx : grid.X(Bounds.Right + dx);
            if (right < left + minimum.Width) right = smooth ? left + minimum.Width : grid.CeilingX(left + minimum.Width);
        }
        if (Edge.Contains('N'))
        {
            top = smooth ? Bounds.Top + dy : grid.Y(Bounds.Top + dy);
            if (top > bottom - minimum.Height) top = smooth ? bottom - minimum.Height : grid.FloorY(bottom - minimum.Height);
        }
        if (Edge.Contains('S'))
        {
            bottom = smooth ? Bounds.Bottom + dy : grid.Y(Bounds.Bottom + dy);
            if (bottom < top + minimum.Height) bottom = smooth ? top + minimum.Height : grid.CeilingY(top + minimum.Height);
        }
        return new(left, top, right - left, bottom - top);
    }
}
