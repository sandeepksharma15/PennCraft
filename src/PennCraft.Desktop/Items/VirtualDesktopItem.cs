namespace PennCraft.Desktop.Items;

/// <summary>Explicitly supported namespace objects; never accept arbitrary shell parsing names as files.</summary>
internal static class VirtualDesktopItem
{
    internal const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    internal static bool IsRecycleBin(string? value) => string.Equals(value, RecycleBin, StringComparison.OrdinalIgnoreCase);
}
