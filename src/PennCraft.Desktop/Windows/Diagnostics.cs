using System.IO;

namespace PennCraft.Desktop.Windows;

internal static class Diagnostics
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PennCraft");
    private static readonly string LogPath = Path.Combine(DirectoryPath, "desktop.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1_048_576)
                File.Move(LogPath, LogPath + ".previous", true);
            File.AppendAllText(LogPath, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
