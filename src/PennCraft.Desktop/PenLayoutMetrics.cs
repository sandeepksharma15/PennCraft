using System.Windows;

namespace PennCraft.Desktop;

/// <summary>Shared tile measurements used by the template and the automatic layout planner.</summary>
public static class PenLayoutMetrics
{
    public const double TileWidth = 88;
    public const double TileHeight = 86;
    public const double TileGap = 2;
    public static Thickness TileMargin => new(TileGap);
    internal const int Columns = 6;
    internal const int MinimumRows = 3;
    internal const double CellWidth = TileWidth + 2 * TileGap;
    internal const double CellHeight = TileHeight + 2 * TileGap;
    internal const double Margin = 24;
    internal static double CommonWidth(double scrollbarWidth) => Math.Max(600, Columns * CellWidth + 20 + scrollbarWidth);
}
