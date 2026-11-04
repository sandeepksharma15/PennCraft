using System.ComponentModel;
using System.IO;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

internal static class ItemRename
{
    public static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Length > 255
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            return "Enter a valid file or folder name without a path.";
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL"
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] is >= '1' and <= '9')
            return "That name is reserved by Windows.";
        return null;
    }

    public static string? Rename(string path, string name)
    {
        var invalid = ValidateName(name);
        if (invalid is not null) return invalid;
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                var parent = Path.GetDirectoryName(path)!;
                var destination = Path.Combine(parent, name);
                if (string.Equals(path, destination, StringComparison.Ordinal)) return null;
                var identity = NativeFileMove.Identity(path);
                if (Directory.EnumerateFileSystemEntries(parent).Any(candidate =>
                        !string.Equals(candidate, path, StringComparison.Ordinal)
                        && string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase)))
                    return "An item with that name already exists. Choose another name.";
                NativeFileMove.Rename(path, destination);
                if (NativeFileMove.Identity(destination) != identity)
                    return "The item changed during rename. Refresh the Pen before trying again.";
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or Win32Exception or ArgumentException)
            {
                return "Windows could not rename this item. Check access and whether another application has it open.";
            }
        }
    }
}
