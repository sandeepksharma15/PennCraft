using System.IO;

namespace PennCraft.Desktop.Items;

/// <summary>Only immediate .lnk files on the Windows shared desktop qualify for an opt-in move.</summary>
internal static class SharedDesktopShortcut
{
    internal static string Folder => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    internal static bool Is(string path, string? folder = null)
    {
        folder ??= Folder;
        return !string.IsNullOrWhiteSpace(folder) && Path.IsPathFullyQualified(path)
            && string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetDirectoryName(PenMembership.NormalizePath(path) ?? string.Empty),
                Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase);
    }

    internal static string[] Enumerate(string? folder = null)
    {
        folder ??= Folder;
        var paths = new List<string>();
        if (!Directory.Exists(folder)) return [];
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                try
                {
                    if (Is(path, folder) && (File.GetAttributes(path)
                        & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) == 0) paths.Add(path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return [.. paths];
    }
}
