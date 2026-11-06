namespace PennCraft.Desktop;

/// <summary>Supported item-content scales, independent of Windows display scaling.</summary>
internal static class PenItemScale
{
    internal static ReadOnlySpan<double> Values => [0.75, 1, 1.25, 1.5];
    internal static double Normalize(double value) => Values.Contains(value) ? value : 1;
}
