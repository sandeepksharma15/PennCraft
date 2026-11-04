using System.IO;

namespace PennCraft.Desktop.Items;

internal sealed record PortalSnapshot(string[] Paths, string? Error);

/// <summary>Reads immediate children only; never creates or changes the linked folder.</summary>
internal static class PortalDirectory
{
    internal static PortalSnapshot Read(string folder)
    {
        try
        {
            if (!Path.IsPathFullyQualified(folder))
                return new([], "The linked folder path is invalid.");
            var paths = Directory.EnumerateFileSystemEntries(folder)
                .Where(path => (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToArray();
            return new(paths, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or System.Security.SecurityException)
        {
            return new([], "The linked folder is unavailable or cannot be read. The connection is retained; retrying automatically.");
        }
    }
}
