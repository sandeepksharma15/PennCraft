using System.Windows;

namespace PennCraft.Desktop;

internal sealed record PenLayoutItem(string Id, Rect CurrentBounds, int ItemCount, double ChromeHeight, bool IsFolded = false);
internal sealed record PenLayoutPlan(Dictionary<string, Rect> Bounds, string? Error);

/// <summary>Pure, ordered column packing in physical pixels. Failure never returns a partial layout.</summary>
internal static class PenLayoutPlanner
{
    internal static PenLayoutPlan Plan(Rect workArea, double scale, double width, IEnumerable<PenLayoutItem> source, Point? gridOrigin = null)
    {
        var items = source.OrderByDescending(item => item.CurrentBounds.Left).ThenBy(item => item.CurrentBounds.Top).ToArray();
        if (workArea.IsEmpty || !Finite(workArea.Left, workArea.Top, workArea.Width, workArea.Height, scale, width)
            || scale <= 0 || width < PenLayoutMetrics.CommonWidth(0)
            || gridOrigin is { } origin && !Finite(origin.X, origin.Y)
            || items.Any(item => string.IsNullOrWhiteSpace(item.Id) || item.ItemCount < 0
                || !Finite(item.CurrentBounds.Left, item.CurrentBounds.Top, item.ChromeHeight) || item.ChromeHeight < 0)
            || items.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            return new([], "The screen or Pen measurements are unavailable. The layout was left unchanged.");
        if (items.Length == 0) return new([], null);
        var grid = ScreenGrid.AtScale(gridOrigin ?? new Point(), scale);
        var margin = 2 * grid.Step;
        var penWidth = grid.LengthUp(width * scale);
        var top = grid.CeilingY(workArea.Top + margin);
        var bottom = grid.FloorY(workArea.Bottom - margin);
        var left = grid.CeilingX(workArea.Left + margin);
        var right = grid.FloorX(workArea.Right - margin);
        var availableHeight = bottom - top;
        var x = right - penWidth;
        var y = top;
        var columns = 1;
        var result = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var minimum = grid.LengthUp((item.IsFolded ? PenSettings.FoldedHeight
                : PenLayoutMetrics.MinimumRows * PenLayoutMetrics.CellHeight + item.ChromeHeight) * scale);
            if (minimum > availableHeight)
                return new([], item.IsFolded
                    ? "The primary screen is too short for a folded Pen header. The layout was left unchanged."
                    : "The primary screen is too short for three icon rows and the Pen header. The layout was left unchanged.");
            var rows = Math.Max(PenLayoutMetrics.MinimumRows, Math.Ceiling(item.ItemCount / (double)PenLayoutMetrics.Columns));
            var height = item.IsFolded ? minimum
                : Math.Min(availableHeight, grid.LengthUp((rows * PenLayoutMetrics.CellHeight + item.ChromeHeight) * scale));
            if (y + height > bottom)
            {
                columns++;
                x -= penWidth + margin;
                y = top;
            }
            result.Add(item.Id, new Rect(x, y, penWidth, height));
            y += height + margin;
        }
        var capacity = Math.Max(0, (int)Math.Floor((right - left + margin) / (penWidth + margin)));
        if (columns > capacity)
            return new([], $"These Pens need {columns} column(s), but the primary screen can fit {capacity} at the six-icon minimum width. Hide some Pens and try again. The layout was left unchanged.");
        return new(result, null);
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
}
