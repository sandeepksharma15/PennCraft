namespace PennCraft.Desktop;

/// <summary>Resize in DIPs while keeping the opposite edge fixed at minimum size.</summary>
public static class PenGeometry
{
    public static PenSettings Resize(PenSettings bounds, string edge, double dx, double dy)
    {
        var left = bounds.Left;
        var top = bounds.Top;
        var width = bounds.Width;
        var height = bounds.Height;
        if (edge.Contains('W'))
        {
            var change = Math.Min(dx, width - PenSettings.MinimumWidth);
            left += change;
            width -= change;
        }
        if (edge.Contains('E')) width = Math.Max(PenSettings.MinimumWidth, width + dx);
        if (edge.Contains('N'))
        {
            var change = Math.Min(dy, height - PenSettings.MinimumHeight);
            top += change;
            height -= change;
        }
        if (edge.Contains('S')) height = Math.Max(PenSettings.MinimumHeight, height + dy);
        return bounds with { Left = left, Top = top, Width = width, Height = height };
    }
}
