using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop.Windows;

internal static class ShellItemLauncher
{
    public static string? Open(string path)
    {
        if (!PenMembership.Exists(path)) return "This item is unavailable. Its reference has been kept in the Pen.";
        try
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            Diagnostics.Write($"open item failed: {exception.GetType().Name}");
            return "Windows could not open this item. Check that it is available and has an associated application.";
        }
    }
}
