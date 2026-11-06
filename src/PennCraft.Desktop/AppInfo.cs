using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace PennCraft.Desktop;

internal static class AppInfo
{
    internal const string AppMutexName = @"Local\PennCraft.App";
    internal const string RepositoryUrl = "https://github.com/sandeepksharma15/PennCraft";
    internal static string Version => typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion.Split('+')[0] ?? "development";
    internal static string DataDirectory => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "PennCraft");

    internal static string? OpenWebsite(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com")
            return "The support link is invalid.";
        try { using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return null; }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return "Windows could not open your browser. Visit " + RepositoryUrl;
        }
    }

    private static Stream IconStream() => typeof(App).Assembly.GetManifestResourceStream("PennCraft.AppIcon")
        ?? throw new InvalidOperationException("The application icon is missing.");

    internal static Icon CreateTrayIcon()
    {
        using var stream = IconStream();
        using var source = new Icon(stream, new Size(32, 32));
        return (Icon)source.Clone();
    }

    internal static BitmapFrame CreateWindowIcon()
    {
        using var stream = IconStream();
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(candidate => candidate.PixelWidth).First();
        frame.Freeze();
        return frame;
    }
}
