using System.IO;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

internal sealed record OrganizationPreview(Dictionary<string, string[]> Groups, string[] Skipped)
{
    internal int Count => Groups.Values.Sum(paths => paths.Length);
}

internal sealed record FirstRunReport(int Moved, string[] Skipped);

/// <summary>One startup attempt. The workspace is durable before the first physical operation.</summary>
internal sealed class FirstRunOrganizer(WorkspaceStore workspace, string desktop, Func<string, FileAttributes>? readAttributes = null)
{
    internal static string? Category(string path, FileAttributes attributes)
    {
        if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) != 0) return null;
        if ((attributes & FileAttributes.Directory) != 0) return "Folders";
        return Path.GetExtension(path).ToLowerInvariant() switch { ".pdf" => "Documents", ".lnk" => "Programs", _ => null };
    }

    public FirstRunReport? Run()
    {
        if (!workspace.IsUninitialized || !Directory.Exists(desktop) || Directory.Exists(Path.Combine(desktop, ".PennCraft"))) return null;
        var preview = Preview();
        var groups = preview.Groups;
        var skipped = preview.Skipped.ToList();
        var ids = groups.Keys.ToDictionary(title => title, _ => Guid.NewGuid().ToString("N"));
        var index = 0;
        foreach (var (title, id) in ids)
        {
            if (!new PenSettingsStore(workspace.SettingsPath(id)).Save(new PenSettings
                { Title = title, Width = 300, Height = 250, Left = 40 + index++ * 320, Top = 60 }))
                throw new IOException("Cannot save first-run Pens.");
        }
        // Publishing consumes the one-time opportunity. A crash never starts a second desktop scan.
        workspace.Save(new WorkspaceSettings(1, [.. ids.Values]));
        var moved = 0;
        foreach (var (title, paths) in groups)
        {
            var id = ids[title];
            var store = new PenSettingsStore(workspace.SettingsPath(id));
            var settings = store.Load();
            var moves = new PenMoveService(desktop, id);
            var recovery = moves.Recover(settings.ItemPaths);
            if (recovery.Error is not null) { skipped.AddRange(paths); continue; }
            foreach (var path in paths)
            {
                var outcome = moves.MoveInto(path);
                if (!outcome.Moved) { skipped.Add(path); continue; }
                settings = settings with { ItemPaths = PenMembership.Normalize(settings.ItemPaths.Append(outcome.Destination)) };
                if (!store.Save(settings))
                    throw new IOException("First-run membership needs move-journal recovery.");
                if (moves.Acknowledge([outcome.OperationId!.Value]) is not null)
                    throw new IOException("First-run move acknowledgement needs recovery.");
                moved++;
            }
        }
        var report = new FirstRunReport(moved, [.. skipped]);
        AtomicJsonFile.Write(workspace.FirstRunReportPath, report);
        return report;
    }

    internal OrganizationPreview Preview(bool includeSharedShortcuts = false)
    {
        var groups = new Dictionary<string, List<string>> { ["Documents"] = [], ["Programs"] = [], ["Folders"] = [] };
        // Inventory first. One unreadable/deleted entry must not stop other categories.
        var skipped = new List<string>();
        foreach (var path in Directory.EnumerateFileSystemEntries(desktop))
        {
            try
            {
                if (Category(path, (readAttributes ?? File.GetAttributes)(path)) is { } category) groups[category].Add(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add(path);
            }
        }
        if (includeSharedShortcuts) groups["Programs"].AddRange(SharedDesktopShortcut.Enumerate());
        return new(groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), [.. skipped]);
    }
}
