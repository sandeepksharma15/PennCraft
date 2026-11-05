using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

internal sealed record PortalMoveIntent(int Version, string? SourceId, string? TargetId,
    string Source, string Destination, string Identity, string? CompletedIdentity = null);

/// <summary>Durable membership handoff around a Windows-managed move. Never replays filesystem operations.</summary>
internal sealed class PortalMoveService(WorkspaceStore workspace, string desktop, string? sharedDesktop = null)
{
    internal string JournalPath => workspace.TransferJournalPath + ".portal";
    public string? BlockedReason { get; private set; }

    public string? Move(string? sourceId, string? targetId, string source, string folder,
        string? expectedIdentity, Action<string, string> move)
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                if (BlockedReason is not null || File.Exists(JournalPath))
                    return BlockedReason ?? "An earlier Portal move needs recovery.";
                var destination = Path.Combine(folder, Path.GetFileName(source));
                var identity = NativeFileMove.Identity(source);
                if (identity is null || expectedIdentity is null || identity != expectedIdentity)
                    return "The source changed or cannot be identified. Refresh and retry.";
                var intent = new PortalMoveIntent(1, sourceId, targetId, source, destination, identity);
                Validate(intent);
                if (sourceId is not null && Read(sourceId) is { PortalFolder: null } sourceSettings
                    && !sourceSettings.ItemPaths.Contains(source, StringComparer.OrdinalIgnoreCase))
                    return "The source Pen no longer contains this item.";
                if (ProtectedRoots.Any(root => Within(root, source)))
                    return "PennCraft storage, settings and their parent folders cannot be moved through a Portal.";
                var attributes = File.GetAttributes(source);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0)
                    return "System items, links and junctions must be moved in File Explorer.";
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
                    || folder.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return "An item cannot be moved into itself or its own subfolder.";
                if (!Directory.Exists(folder)) return "The destination folder is unavailable.";
                if (Directory.EnumerateFileSystemEntries(folder).Any(path =>
                    string.Equals(Path.GetFileName(path), Path.GetFileName(source), StringComparison.OrdinalIgnoreCase)))
                    return "That name already exists at the destination. Both items were retained.";
                // Check that any Pen settings can be saved before changing files.
                Preflight(sourceId);
                Preflight(targetId);
                AtomicJsonFile.Write(JournalPath, intent);
                move(source, destination);
                if (PenMembership.Exists(source) || !PenMembership.Exists(destination))
                    throw new IOException("The Windows move did not complete.");
                var completed = NativeFileMove.Identity(destination) ?? throw new IOException("Cannot identify destination.");
                intent = intent with { CompletedIdentity = completed };
                AtomicJsonFile.Write(JournalPath, intent);
                Commit(intent);
                File.Delete(JournalPath);
                return null;
            }
            catch (Exception exception) when (IsExpected(exception))
            {
                Diagnostics.Write($"Portal move stopped: {exception.GetType().Name}");
                // Even cancellation can leave partial cross-volume results; reconcile actual locations.
                var recovery = Recover();
                return recovery ?? (exception is OperationCanceledException
                    ? "Windows cancelled the operation. Locations were reconciled; check both containers."
                    : "Windows stopped the operation. Locations were reconciled; check the source and destination.");
            }
        }
    }

    public string? Recover()
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                if (!File.Exists(JournalPath)) return BlockedReason;
                if (new FileInfo(JournalPath).LinkTarget is not null) throw new InvalidDataException("Linked journal.");
                var intent = JsonSerializer.Deserialize<PortalMoveIntent>(File.ReadAllText(JournalPath))
                    ?? throw new InvalidDataException("Invalid Portal move journal.");
                Validate(intent);
                var sourceExists = PenMembership.Exists(intent.Source);
                var targetExists = PenMembership.Exists(intent.Destination);
                if (sourceExists && !targetExists && NativeFileMove.Identity(intent.Source) == intent.Identity)
                {
                    File.Delete(JournalPath); // No physical replay or speculative deletion.
                    return null;
                }
                if (sourceExists || !targetExists
                    || NativeFileMove.Identity(intent.Destination) != (intent.CompletedIdentity ?? intent.Identity))
                    throw new InvalidDataException("Ambiguous Portal move.");
                Commit(intent);
                File.Delete(JournalPath);
                return null;
            }
            catch (Exception exception) when (IsExpected(exception))
            {
                Diagnostics.Write($"Portal move recovery stopped: {exception.GetType().Name}");
                return BlockedReason = "An interrupted Portal move needs review. Files and the recovery record were retained; item operations are paused.";
            }
        }
    }

    private void Validate(PortalMoveIntent intent)
    {
        if (intent.Version != 1 || string.IsNullOrWhiteSpace(intent.Identity)
            || PenMembership.NormalizePath(intent.Source) != intent.Source
            || PenMembership.NormalizePath(intent.Destination) != intent.Destination
            || !string.Equals(Path.GetFileName(intent.Source), Path.GetFileName(intent.Destination), StringComparison.Ordinal))
            throw new InvalidDataException("Invalid Portal move.");
        ValidateScope(intent.SourceId, intent.Source, source: true);
        ValidateScope(intent.TargetId, intent.Destination);
    }

    private void ValidateScope(string? id, string path, bool source = false)
    {
        if (id is null && source && SharedDesktopShortcut.Is(path, sharedDesktop)) return;
        var folder = desktop;
        if (id is not null)
        {
            var settings = Read(id);
            folder = settings.PortalFolder ?? new PenMoveService(desktop, id).StorageDirectory;
            if (settings.PortalFolder is not null && ProtectedRoots.Any(root => Within(folder, root)))
                throw new InvalidDataException("Portal file operations cannot target PennCraft internal storage.");
        }
        if (!string.Equals(Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar),
            folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Move outside its container.");
    }

    private string[] ProtectedRoots => [Path.Combine(desktop, ".PennCraft"), Path.GetDirectoryName(workspace.TransferJournalPath)!];

    private static bool Within(string path, string root) =>
        string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private PenSettings Read(string id)
    {
        if (!WorkspaceStore.IsValidId(id) || !workspace.Load().PenIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unknown container.");
        return JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(workspace.SettingsPath(id)))
            ?? throw new InvalidDataException("Missing container settings.");
    }

    private void Preflight(string? id)
    {
        if (id is not null && !new PenSettingsStore(workspace.SettingsPath(id)).Save(Read(id)))
            throw new IOException("Cannot save container settings.");
    }

    private void Commit(PortalMoveIntent intent)
    {
        UpdateMembership(intent.TargetId, intent.Destination, add: true, replacedSource: intent.Source);
        UpdateMembership(intent.SourceId, intent.Source, add: false);
    }

    private void UpdateMembership(string? id, string path, bool add, string? replacedSource = null)
    {
        if (id is null) return;
        var settings = Read(id);
        if (settings.PortalFolder is not null) return;
        var paths = add ? PenMembership.Normalize(settings.ItemPaths.Select(existing =>
            string.Equals(existing, replacedSource, StringComparison.OrdinalIgnoreCase) ? path : existing).Append(path))
            : PenMembership.Remove(settings.ItemPaths, path);
        if (!new PenSettingsStore(workspace.SettingsPath(id)).Save(settings with
            { ItemPaths = paths, TrackedItems = ItemTracking.Capture(paths, settings.TrackedItems) }))
            throw new IOException("Cannot commit Pen membership.");
    }

    private static bool IsExpected(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or System.ComponentModel.Win32Exception or ArgumentException or JsonException or COMException or OperationCanceledException or NotSupportedException;
}
