using System.IO;
using System.Text.Json.Serialization;
using System.Windows;

namespace PennCraft.Desktop;

internal sealed record PixelBounds(
    [property: JsonRequired] double X, [property: JsonRequired] double Y,
    [property: JsonRequired] double Width, [property: JsonRequired] double Height)
{
    internal Rect ToRect() => new(X, Y, Width, Height);
    internal static PixelBounds From(Rect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
}

/// <summary>Presentation only. Never captures item membership, titles or folder connections.</summary>
internal sealed record LayoutPresentation(
    [property: JsonRequired] double Left, [property: JsonRequired] double Top,
    [property: JsonRequired] double Width, [property: JsonRequired] double Height,
    [property: JsonRequired] bool IsVisible, [property: JsonRequired] bool IsFolded,
    [property: JsonRequired] double ItemScale, [property: JsonRequired] PixelBounds Bounds)
{
    internal static LayoutPresentation Capture(PenSettings settings, Rect bounds) => new(
        settings.Left, settings.Top, settings.Width, settings.Height,
        settings.IsVisible, settings.IsFolded, settings.ItemScale, PixelBounds.From(bounds));

    internal PenSettings Merge(PenSettings current) => current with
    {
        Left = Left, Top = Top, Width = Width, Height = Height,
        IsVisible = IsVisible, IsFolded = IsFolded, ItemScale = ItemScale
    };

    internal void Validate()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top) || Math.Abs(Left) > 10_000_000 || Math.Abs(Top) > 10_000_000
            || !double.IsFinite(Width) || Width < PenSettings.MinimumWidth || Width > 10000
            || !double.IsFinite(Height) || Height < PenSettings.MinimumHeight || Height > 10000
            || PenItemScale.Normalize(ItemScale) != ItemScale || Bounds is null
            || !double.IsFinite(Bounds.X) || !double.IsFinite(Bounds.Y)
            || Math.Abs(Bounds.X) > 10_000_000 || Math.Abs(Bounds.Y) > 10_000_000
            || !double.IsFinite(Bounds.Width) || !double.IsFinite(Bounds.Height)
            || Bounds.Width < 1 || Bounds.Width > 100000 || Bounds.Height < 1 || Bounds.Height > 100000)
            throw new InvalidDataException("A saved layout has invalid presentation data.");
    }
}

internal sealed record SavedLayout(
    [property: JsonRequired] string Id, [property: JsonRequired] string Name,
    [property: JsonRequired] string DisplayStamp,
    [property: JsonRequired] Dictionary<string, LayoutPresentation> Pens);

internal sealed record SavedLayoutPlan(Dictionary<string, LayoutPresentation> Pens, int MissingCount, string? Error);

internal static class SavedLayoutPlanner
{
    internal static SavedLayoutPlan Plan(SavedLayout layout, string displayStamp, IEnumerable<string> currentIds, Rect[] workAreas)
    {
        if (layout.DisplayStamp != displayStamp)
            return new([], 0, "The monitor setup or display scaling changed. Restore the original setup or save a new layout. Nothing was changed.");
        var ids = currentIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = layout.Pens.Where(pair => ids.Contains(pair.Key)).ToArray();
        var missing = layout.Pens.Count - selected.Length;
        if (selected.Length == 0) return new([], missing, "None of the saved containers still exist. Nothing was changed.");
        if (workAreas.Length == 0 || workAreas.Any(area => area.IsEmpty || area.Width <= 0 || area.Height <= 0
            || !double.IsFinite(area.X) || !double.IsFinite(area.Y) || !double.IsFinite(area.Width) || !double.IsFinite(area.Height)))
            return new([], missing, "Windows could not determine the monitor work areas. Nothing was changed.");
        var result = new Dictionary<string, LayoutPresentation>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, presentation) in selected)
        {
            presentation.Validate();
            var bounds = presentation.Bounds.ToRect();
            // Pick the nearest monitor that can contain the window; preserve valid positions exactly.
            var area = workAreas.Where(area => bounds.Width <= area.Width && bounds.Height <= area.Height)
                .OrderBy(area => Distance(bounds, area)).FirstOrDefault(Rect.Empty);
            if (area.IsEmpty)
                return new([], missing, "A saved container is larger than the available work areas. Nothing was changed.");
            var safe = new Rect(Math.Clamp(bounds.X, area.Left, area.Right - bounds.Width),
                Math.Clamp(bounds.Y, area.Top, area.Bottom - bounds.Height), bounds.Width, bounds.Height);
            result.Add(id, presentation with { Bounds = PixelBounds.From(safe) });
        }
        return new(result, missing, null);
    }

    private static double Distance(Rect bounds, Rect area)
    {
        var x = Math.Clamp(bounds.Left, area.Left, area.Right - bounds.Width) - bounds.Left;
        var y = Math.Clamp(bounds.Top, area.Top, area.Bottom - bounds.Height) - bounds.Top;
        return x * x + y * y;
    }
}
