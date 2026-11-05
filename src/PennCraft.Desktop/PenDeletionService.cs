using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

internal enum DeletionCheckpoint { IntentSaved, StorageRemoved, SettingsRemoved }
internal sealed record PenDeletionJournal([property: JsonRequired] int Version, [property: JsonRequired] string? PendingId, bool IsPortal = false);
internal sealed record PenDeletionResult(bool Removed, string? Error);

/// <summary>Deletes only verified-empty Pen metadata. Every directory removal is non-recursive.</summary>
internal sealed class PenDeletionService(WorkspaceStore workspace, string desktop, Action<DeletionCheckpoint>? checkpoint = null)
{
    internal string JournalPath => workspace.TransferJournalPath + ".deletion";
    internal string? BlockedReason { get; private set; }

    internal PenDeletionResult Delete(string id, bool portal = false)
    {
        lock (PenMoveService.FileOperationGate)
        {
            var prepared = false;
            var journalChecked = false;
            var removed = false;
            try
            {
                if (BlockedReason is not null || ReadJournal() is not null)
                    return new(false, "An earlier Pen deletion needs recovery. Restart before deleting another Pen.");
                journalChecked = true;
                var index = workspace.Load();
                if (!index.PenIds.Contains(id, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Unknown Pen.");
                ValidateEmpty(id, portal, allowMissingSettings: false);
                WriteJournal(id, portal);
                prepared = true;
                checkpoint?.Invoke(DeletionCheckpoint.IntentSaved);
                Finish(id, portal, () => removed = true);
                WriteJournal(null);
                return new(true, null);
            }
            catch (Exception exception) when (Expected(exception))
            {
                if (prepared || !journalChecked && File.Exists(JournalPath)) BlockedReason = "An interrupted Pen deletion needs recovery. Remaining files were retained; restart PennCraft before more item operations.";
                return new(removed, BlockedReason ?? "This Pen could not be deleted. Check its storage for files, recovery records or access restrictions.");
            }
        }
    }

    internal string? Recover()
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                var pending = ReadJournal();
                if (pending is null) return null;
                Finish(pending.PendingId!, pending.IsPortal, () => { });
                WriteJournal(null);
                return null;
            }
            catch (Exception exception) when (Expected(exception))
            {
                return BlockedReason = "An interrupted Pen deletion needs review. Remaining files and its deletion record were retained; item operations are paused.";
            }
        }
    }

    private void Finish(string id, bool portal, Action indexRemoved)
    {
        ValidateEmpty(id, portal, allowMissingSettings: true);
        var moves = new PenMoveService(desktop, id);
        var directory = Path.GetDirectoryName(moves.StorageDirectory)!;
        if (Directory.Exists(moves.StorageDirectory)) Directory.Delete(moves.StorageDirectory);
        if (File.Exists(moves.JournalPath))
        {
            if (MoveJournalFile.Read(moves.JournalPath).Length != 0) throw new InvalidDataException("Move pending.");
            File.Delete(moves.JournalPath);
        }
        if (Directory.Exists(directory)) Directory.Delete(directory);
        checkpoint?.Invoke(DeletionCheckpoint.StorageRemoved);
        File.Delete(workspace.SettingsPath(id));
        checkpoint?.Invoke(DeletionCheckpoint.SettingsRemoved);
        var index = workspace.Load();
        workspace.Save(index with { PenIds = index.PenIds.Where(item =>
            !string.Equals(item, id, StringComparison.OrdinalIgnoreCase)).ToArray() });
        indexRemoved();
    }

    private void ValidateEmpty(string id, bool portal, bool allowMissingSettings)
    {
        if (!WorkspaceStore.IsValidId(id)) throw new InvalidDataException("Invalid Pen identity.");
        var path = workspace.SettingsPath(id);
        CheckNotLink(Path.GetDirectoryName(workspace.TransferJournalPath)!);
        CheckNotLink(Path.GetDirectoryName(path)!);
        CheckNotLink(path);
        if (Directory.Exists(path)) throw new InvalidDataException("Settings path is a directory.");
        if (File.Exists(path))
        {
            var settings = JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Invalid Pen settings.");
            if ((settings.PortalFolder is not null) != portal || settings.ItemPaths is not { Length: 0 })
                throw new InvalidDataException("Pen is not empty or is a Portal.");
        }
        else if (!allowMissingSettings) throw new InvalidDataException("Missing Pen settings.");
        ValidateWorkspaceMoves();
        var moves = new PenMoveService(desktop, id);
        var directory = Path.GetDirectoryName(moves.StorageDirectory)!;
        CheckNotLink(Path.Combine(desktop, ".PennCraft"));
        CheckNotLink(directory);
        CheckNotLink(moves.StorageDirectory);
        CheckNotLink(moves.JournalPath);
        if (File.Exists(directory) || File.Exists(moves.StorageDirectory)) throw new InvalidDataException("Unexpected storage file.");
        if (!Directory.Exists(directory)) return;
        if (moves.PrepareTransferStorage(create: false) is not null) throw new InvalidDataException("Storage cannot be verified.");
        if (Directory.Exists(moves.StorageDirectory) && Directory.EnumerateFileSystemEntries(moves.StorageDirectory).Any())
            throw new InvalidDataException("Untracked storage contents.");
        if (Directory.EnumerateFileSystemEntries(directory).Any(entry =>
            !string.Equals(entry, moves.StorageDirectory, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entry, moves.JournalPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Unknown Pen metadata.");
    }

    private void ValidateWorkspaceMoves()
    {
        var transfer = workspace.TransferJournalPath;
        CheckNotLink(transfer);
        if (File.Exists(transfer))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(transfer));
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number) || number != 1
                || !json.RootElement.TryGetProperty("Pending", out var pending) || pending.ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Workspace transfer pending.");
        }
        if (File.Exists(transfer + ".portal")) throw new InvalidDataException("Portal move pending.");
    }

    private PenDeletionJournal? ReadJournal()
    {
        CheckNotLink(JournalPath);
        if (!File.Exists(JournalPath)) return null;
        var journal = JsonSerializer.Deserialize<PenDeletionJournal>(File.ReadAllText(JournalPath));
        if (journal is not { Version: 1 } || journal.PendingId is not null && !WorkspaceStore.IsValidId(journal.PendingId))
            throw new InvalidDataException("Invalid deletion record.");
        return journal.PendingId is null ? null : journal;
    }

    private void WriteJournal(string? id, bool portal = false) => AtomicJsonFile.Write(JournalPath, new PenDeletionJournal(1, id, portal));

    private static void CheckNotLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Deletion paths must not be links.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    private static bool Expected(Exception exception) => exception is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or InvalidOperationException;
}
