using System.ComponentModel;
using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

internal sealed record MoveOutcome(string Source, string Destination, Guid? OperationId, string? Error)
{
    public bool Moved => OperationId.HasValue && Error is null;
}

internal sealed record MoveRecovery(string[] ItemPaths, Guid[] ResolvedIds, string? Error);

/// <summary>Owns desktop-to-storage moves and their recovery journal. Use from one serialized worker.</summary>
internal sealed class PenMoveService
{
    private static readonly object MoveGate = new();
    private const string OwnerText = "PennCraft managed desktop storage v1";
    private readonly string desktop;
    private readonly string root;
    private readonly string owner;
    private MoveEntry[] entries = [];
    private string? blockedReason;
    private bool recovered;

    public string StorageDirectory { get; }
    internal string JournalPath { get; }

    public PenMoveService(string desktopDirectory, string penId = "Default")
    {
        if (!WorkspaceStore.IsValidId(penId)) throw new ArgumentException("Invalid Pen identity.", nameof(penId));
        desktop = PenMembership.NormalizePath(desktopDirectory)
            ?? throw new ArgumentException("An absolute desktop path is required.", nameof(desktopDirectory));
        root = Path.Combine(desktop, ".PennCraft");
        owner = Path.Combine(root, "owner.txt");
        StorageDirectory = Path.Combine(root, penId, "Items");
        JournalPath = Path.Combine(root, penId, "moves.json");
    }

    public bool IsManaged(string path) => IsChild(path, StorageDirectory);

    public MoveRecovery Recover(IEnumerable<string> membership)
    {
        recovered = true;
        var paths = PenMembership.Normalize(membership);
        var resolved = new List<Guid>();
        try
        {
            if (!Directory.Exists(root)) return new(paths, [], null);
            ValidateStorage();
            entries = MoveJournalFile.Read(JournalPath);
            if (entries.Any(entry => !ValidEntry(entry)) || entries.Select(entry => entry.Id).Distinct().Count() != entries.Length)
                throw new InvalidDataException("The move journal contains invalid entries.");
            foreach (var entry in entries)
            {
                var sourceExists = PenMembership.Exists(entry.Source);
                var destinationExists = PenMembership.Exists(entry.Destination);
                if (sourceExists && !destinationExists && NativeFileMove.Identity(entry.Source) == entry.Identity)
                {
                    resolved.Add(entry.Id); // Prepared but not applied; never replay a physical move automatically.
                }
                else if (!sourceExists && destinationExists && NativeFileMove.Identity(entry.Destination) == entry.Identity)
                {
                    paths = Apply(paths, entry);
                    resolved.Add(entry.Id);
                }
                else
                {
                    blockedReason = "An interrupted move needs review. Files were left untouched; inspect Pen storage and its move journal.";
                }
            }
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            blockedReason = "Move recovery could not be completed. Files were left untouched; check Pen storage permissions and its move journal.";
        }
        return new(paths, [.. resolved], blockedReason);
    }

    public MoveOutcome MoveInto(string source)
    {
        source = PenMembership.NormalizePath(source) ?? string.Empty;
        if (!IsChild(source, desktop))
        {
            if (IsManaged(source)) return new(source, source, null, null);
            if (source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, root, StringComparison.OrdinalIgnoreCase))
                return new(source, source, null, "Pen storage cannot be added as an item.");
            return new(source, source, null, PenMembership.Exists(source) ? null : "This item is unavailable.");
        }
        if (string.Equals(source, root, StringComparison.OrdinalIgnoreCase))
            return new(source, source, null, "Pen storage cannot be moved into itself.");
        return Move(source, Path.Combine(StorageDirectory, Path.GetFileName(source)), intoPen: true);
    }

    public MoveOutcome MoveOut(string source)
    {
        if (!IsManaged(source)) return new(source, source, null, null); // Legacy/external reference only.
        return Move(source, Path.Combine(desktop, Path.GetFileName(source)), intoPen: false);
    }

    private MoveOutcome Move(string source, string destination, bool intoPen)
    {
        // Different Pens share the desktop source/destination and storage ownership marker.
        lock (MoveGate) return MoveCore(source, destination, intoPen);
    }

    private MoveOutcome MoveCore(string source, string destination, bool intoPen)
    {
        if (!recovered) return new(source, destination, null, "Startup recovery must finish before moving items.");
        if (blockedReason is not null) return new(source, destination, null, blockedReason);
        try
        {
            EnsureStorage();
            if (!PenMembership.Exists(source)) return new(source, destination, null, "This item is unavailable.");
            var expectedParent = intoPen ? desktop : StorageDirectory;
            if (NativeFileMove.Identity(Path.GetDirectoryName(source)!) != NativeFileMove.Identity(expectedParent))
                throw new InvalidDataException("The source is outside the authorized move directory.");
            if (PenMembership.Exists(destination) || Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(destination)!)
                    .Any(path => string.Equals(Path.GetFileName(path), Path.GetFileName(destination), StringComparison.OrdinalIgnoreCase)))
                return new(source, destination, null, $"An item named '{Path.GetFileName(destination)}' already exists at the destination. Rename one item and retry.");
            var attributes = File.GetAttributes(source);
            FileSystemInfo item = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(source) : new FileInfo(source);
            if ((attributes & FileAttributes.System) != 0 || item.LinkTarget is not null)
                return new(source, destination, null, "System items, symbolic links and junctions are not moved.");
            var entry = new MoveEntry(Guid.NewGuid(), source, destination, NativeFileMove.Identity(source), intoPen);
            var next = entries.Append(entry).ToArray();
            MoveJournalFile.Write(JournalPath, next);
            entries = next;
            NativeFileMove.Rename(source, destination);
            if (PenMembership.Exists(source) || NativeFileMove.Identity(destination) != entry.Identity)
                throw new IOException("The item changed during the move.");
            return new(source, destination, entry.Id, null);
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            // Do not retry or guess after a native/filesystem failure. The journal resolves it on restart.
            blockedReason = "The move could not be completed. The item was kept or recorded for recovery. Restart PennCraft after checking access and the destination.";
            Diagnostics.Write($"item move failed: {exception.GetType().Name}");
            return new(source, destination, null, blockedReason);
        }
    }

    public string? Acknowledge(IEnumerable<Guid> ids)
    {
        var completed = ids.ToHashSet();
        if (completed.Count == 0) return null;
        try
        {
            ValidateStorage();
            var remaining = entries.Where(entry => !completed.Contains(entry.Id)).ToArray();
            MoveJournalFile.Write(JournalPath, remaining);
            entries = remaining;
            return null;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            blockedReason = "The item moved, but recovery bookkeeping could not be saved. Restart PennCraft to reconcile it.";
            return blockedReason;
        }
    }

    public void BlockUntilRestart() => blockedReason = "Settings could not be saved. Further moves are paused until restart and recovery.";

    internal static string[] Apply(IEnumerable<string> paths, MoveEntry entry) => entry.IntoPen
        ? PenMembership.Normalize(paths.Select(path => string.Equals(path, entry.Source, StringComparison.OrdinalIgnoreCase)
            ? entry.Destination : path).Append(entry.Destination))
        : PenMembership.Remove(paths, entry.Source);

    private bool ValidEntry(MoveEntry? entry) => entry is not null && entry.Id != Guid.Empty
        && !string.IsNullOrWhiteSpace(entry.Identity)
        && (entry.IntoPen
            ? IsChild(entry.Source, desktop) && IsChild(entry.Destination, StorageDirectory)
            : IsChild(entry.Source, StorageDirectory) && IsChild(entry.Destination, desktop))
        && string.Equals(Path.GetFileName(entry.Source), Path.GetFileName(entry.Destination), StringComparison.OrdinalIgnoreCase)
        && !string.Equals(entry.Source, root, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(entry.Destination, root, StringComparison.OrdinalIgnoreCase);

    private static bool IsChild(string? path, string parent)
    {
        var normalized = PenMembership.NormalizePath(path);
        return normalized is not null && string.Equals(Path.GetDirectoryName(normalized), parent, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureStorage()
    {
        if (!Directory.Exists(root))
        {
            Directory.CreateDirectory(root);
            using (var stream = new FileStream(owner, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(OwnerText);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.Hidden);
        }
        // Refuse to take over an existing unowned directory.
        if (!File.Exists(owner) || File.ReadAllText(owner) != OwnerText)
            throw new InvalidDataException("This directory is not owned by PennCraft.");
        CheckNotLink(root);
        Directory.CreateDirectory(Path.GetDirectoryName(StorageDirectory)!);
        CheckNotLink(Path.GetDirectoryName(StorageDirectory)!);
        Directory.CreateDirectory(StorageDirectory);
        ValidateStorage();
    }

    private void ValidateStorage()
    {
        if (!File.Exists(owner) || File.ReadAllText(owner) != OwnerText)
            throw new InvalidDataException("This directory is not owned by PennCraft.");
        CheckNotLink(root);
        CheckNotLink(Path.GetDirectoryName(StorageDirectory)!);
        CheckNotLink(StorageDirectory);
        if (File.Exists(JournalPath) && new FileInfo(JournalPath).LinkTarget is not null)
            throw new InvalidDataException("The move journal cannot be a link.");
    }

    private static void CheckNotLink(string path)
    {
        if (Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Storage cannot be redirected through a link.");
    }

    private static bool IsStorageError(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or Win32Exception or JsonException or ArgumentException or NotSupportedException;
}
