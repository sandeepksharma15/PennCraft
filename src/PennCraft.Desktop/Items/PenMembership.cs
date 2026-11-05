using System.IO;

namespace PennCraft.Desktop.Items;

/// <summary>Ordered path references. No operation here moves, copies or deletes a file.</summary>
public static class PenMembership
{
    public static string[] Normalize(IEnumerable<string>? paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var path in paths ?? [])
        {
            var normalized = NormalizePath(path);
            if (normalized is not null && seen.Add(normalized)) result.Add(normalized);
        }
        return [.. result];
    }

    public static string? NormalizePath(string? path)
    {
        if (VirtualDesktopItem.IsRecycleBin(path)) return VirtualDesktopItem.RecycleBin;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static bool Exists(string path) => VirtualDesktopItem.IsRecycleBin(path) || File.Exists(path) || Directory.Exists(path);

    public static string[] Add(IEnumerable<string> current, IEnumerable<string> dropped) =>
        Normalize(current.Concat(Normalize(dropped).Where(Exists)));

    public static string[] Remove(IEnumerable<string> current, string path) =>
        current.Where(item => !StringComparer.OrdinalIgnoreCase.Equals(item, path)).ToArray();
}
