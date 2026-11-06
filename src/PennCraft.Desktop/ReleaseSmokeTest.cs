using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop;

/// <summary>Release verification uses only a new temporary workspace and never starts desktop organization.</summary>
internal static class ReleaseSmokeTest
{
    internal static async Task<int> RunAsync(string reportPath)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PennCraft-smoke-" + Guid.NewGuid().ToString("N")));
        MainWindow? window = null;
        try
        {
            if (!Path.IsPathFullyQualified(reportPath) || File.Exists(reportPath))
                throw new ArgumentException("Use a new absolute smoke-report path.");
            Directory.CreateDirectory(root);
            var workspace = new WorkspaceStore(root);
            FirstRunSetup.StartEmpty(workspace);
            var store = new PenSettingsStore(workspace.SettingsPath("Default"));
            var item = Path.Combine(root, "Smoke.txt");
            File.WriteAllText(item, "release smoke test");
            if (!store.Save(store.Load() with { ItemPaths = [item] })) throw new IOException("Smoke settings write failed.");
            using var icon = AppInfo.CreateTrayIcon();
            window = new MainWindow(store, "Default", root);
            window.Show();
            await window.ItemsReady;
            if (!window.SetItemScale(1.25) || !window.ToggleFold()) throw new InvalidOperationException("Smoke presentation failed.");
            var original = window.CapturePresentation();
            var catalog = new SavedLayoutStore(workspace.SavedLayoutsPath);
            var saved = catalog.Add("Smoke", DesktopGeometry.DisplayLayoutStamp(), new() { ["Default"] = original });
            if (catalog.Load().Single().Id != saved.Id) throw new InvalidDataException("Smoke catalog read failed.");
            var transaction = new SavedLayoutTransaction(workspace);
            window.BeginLayout();
            try
            {
                if (!transaction.Execute(new() { ["Default"] = original }, () =>
                {
                    window.ApplyPresentation(original with { IsVisible = false });
                    return new() { ["Default"] = window.CapturePresentation() };
                }, () => window.ApplyPresentation(original))) throw new IOException("Smoke restore failed.");
            }
            finally { window.EndLayout(); }
            if (!store.Load().IsFolded || store.Load().IsVisible || File.ReadAllText(item) != "release smoke test")
                throw new InvalidDataException("Smoke state verification failed.");
            var runtimeLocal = File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"));
            if (!runtimeLocal) throw new InvalidOperationException("The release must contain its .NET runtime.");
            return WriteReport(reportPath, new { Success = true, Version = AppInfo.Version, SelfContained = runtimeLocal }) ? 0 : 1;
        }
        catch (Exception exception)
        {
            WriteReport(reportPath, new { Success = false, Error = exception.ToString() });
            return 1;
        }
        finally
        {
            window?.ExitApplication();
            // Only this invocation's own GUID directory directly beneath the temporary directory.
            if (Path.GetDirectoryName(root)?.TrimEnd(Path.DirectorySeparatorChar)
                == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                && Path.GetFileName(root).StartsWith("PennCraft-smoke-", StringComparison.Ordinal))
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal static bool WriteReport(string path, object payload)
    {
        if (!Path.IsPathFullyQualified(path) || File.Exists(path)) return false;
        try
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent)) return false;
            Directory.CreateDirectory(parent);
            using var report = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(report, payload);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
