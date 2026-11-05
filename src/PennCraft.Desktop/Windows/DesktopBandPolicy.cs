namespace PennCraft.Desktop.Windows;

internal sealed record DesktopBandWindow(nint Handle, bool IsDesktop);

/// <summary>A stable Pen band stays below applications and above/beside desktop shell surfaces.</summary>
internal static class DesktopBandPolicy
{
    internal static bool NeedsRepair(nint[] expectedPens, DesktopBandWindow[] topToBottom)
    {
        var pens = expectedPens.ToHashSet();
        if (!topToBottom.Where(window => pens.Contains(window.Handle)).Select(window => window.Handle).SequenceEqual(expectedPens))
            return true;
        var seenPen = false;
        foreach (var window in topToBottom)
        {
            if (pens.Contains(window.Handle)) seenPen = true;
            else if (seenPen && !window.IsDesktop) return true;
        }
        return false;
    }
}
