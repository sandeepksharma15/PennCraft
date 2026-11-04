using System.ComponentModel;
using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

internal enum TransferCheckpoint { IntentSaved, RenameApplied, TargetSaved, SourceSaved }
internal sealed record PenTransferIntent(Guid Id, string SourcePenId, string TargetPenId,
    string SourcePath, string DestinationPath, string? Identity, bool Physical);
internal sealed record PenTransferJournal(int Version, PenTransferIntent? Pending);
internal sealed record PenTransferResult(PenSettings Source, PenSettings Target, bool Completed, bool NeedsRecovery, string? Error);

/// <summary>One write-ahead transaction for a file/reference and two independent settings snapshots.</summary>
internal sealed class PenTransferService(WorkspaceStore workspace, string desktop, Action<TransferCheckpoint>? checkpoint = null)
{
    public string? BlockedReason { get; private set; }
    internal string JournalPath => workspace.TransferJournalPath;

    public PenTransferResult Transfer(string sourceId, string targetId, string path, string? expectedIdentity,
        PenSettings source, PenSettings target, PenMoveService sourceMoves, PenMoveService targetMoves)
    {
        lock (PenMoveService.FileOperationGate)
        {
            var prepared = false;
            var journalChecked = false;
            try
            {
                if (BlockedReason is not null) return Failure(source, target, BlockedReason, true);
                if (ReadJournal() is not null) return Failure(source, target, "A previous transfer needs startup recovery.", true);
                journalChecked = true;
                ValidateIds(sourceId, targetId);
                path = PenMembership.NormalizePath(path) ?? throw new InvalidDataException("Invalid source path.");
                if (!source.ItemPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    return Failure(source, target, "The source Pen no longer contains this item.");
                var physical = sourceMoves.IsManaged(path);
                var destination = physical ? Path.Combine(targetMoves.StorageDirectory, Path.GetFileName(path)) : path;
                if (target.ItemPaths.Contains(destination, StringComparer.OrdinalIgnoreCase))
                    return Failure(source, target, "This item or filename is already present in the destination Pen.");
                if (sourceMoves.HasUnresolvedMoves || targetMoves.HasUnresolvedMoves)
                    return Failure(source, target, "Finish move recovery in both Pens before transferring.");
                if (physical)
                {
                    var error = sourceMoves.PrepareTransferStorage(create: false)
                        ?? targetMoves.PrepareTransferStorage(create: true);
                    if (error is not null) return Failure(source, target, error);
                    if (PenMembership.Exists(destination) || Directory.EnumerateFileSystemEntries(targetMoves.StorageDirectory)
                        .Any(item => string.Equals(Path.GetFileName(item), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                        return Failure(source, target, "An item with that filename already exists in the destination Pen.");
                    var attributes = File.GetAttributes(path);
                    FileSystemInfo item = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
                    if ((attributes & FileAttributes.System) != 0 || item.LinkTarget is not null)
                        return Failure(source, target, "System items, symbolic links and junctions are not transferred.");
                    if (expectedIdentity is null || NativeFileMove.Identity(path) != expectedIdentity
                        || NativeFileMove.Identity(Path.GetDirectoryName(path)!) != NativeFileMove.Identity(sourceMoves.StorageDirectory))
                        return Failure(source, target, "The source item changed. Refresh the Pen and try again.");
                }

                // Preflight both stores before creating durable intent or changing a filesystem item.
                Save(sourceId, source);
                Save(targetId, target);
                var identity = expectedIdentity ?? source.TrackedItems.FirstOrDefault(entry =>
                    string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))?.Identity;
                var intent = new PenTransferIntent(Guid.NewGuid(), sourceId, targetId, path, destination, identity, physical);
                ValidateIntent(intent);
                WriteJournal(intent);
                prepared = true;
                checkpoint?.Invoke(TransferCheckpoint.IntentSaved);
                if (physical)
                {
                    NativeFileMove.Rename(path, destination);
                    if (PenMembership.Exists(path) || NativeFileMove.Identity(destination) != expectedIdentity)
                        throw new IOException("The transfer could not be verified.");
                }
                checkpoint?.Invoke(TransferCheckpoint.RenameApplied);
                var pair = Apply(intent, source, target);
                Save(targetId, pair.Target);
                checkpoint?.Invoke(TransferCheckpoint.TargetSaved);
                Save(sourceId, pair.Source);
                checkpoint?.Invoke(TransferCheckpoint.SourceSaved);
                if (physical && (PenMembership.Exists(path) || NativeFileMove.Identity(destination) != expectedIdentity))
                    throw new IOException("The item changed before the transfer committed.");
                WriteJournal(null);
                return new(pair.Source, pair.Target, true, false, null);
            }
            catch (Exception exception) when (IsTransferError(exception))
            {
                Diagnostics.Write($"Pen transfer failed: {exception.GetType().Name}");
                var recovery = prepared || !journalChecked && File.Exists(JournalPath);
                return Failure(source, target, recovery
                    ? "The transfer is recorded for recovery. Restart PennCraft before more item operations."
                    : "The transfer could not start. Check permissions, locks and the destination; the source was retained.", recovery);
            }
        }
    }

    public string? Recover()
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                var intent = ReadJournal();
                if (intent is null) return null;
                ValidateIntent(intent);
                var source = ReadStrict(intent.SourcePenId);
                var target = ReadStrict(intent.TargetPenId);
                if (intent.Physical)
                {
                    var sourceMoves = new PenMoveService(desktop, intent.SourcePenId);
                    var targetMoves = new PenMoveService(desktop, intent.TargetPenId);
                    var error = sourceMoves.PrepareTransferStorage(create: false)
                        ?? targetMoves.PrepareTransferStorage(create: false);
                    if (error is not null) throw new InvalidDataException(error);
                    var sourceExists = PenMembership.Exists(intent.SourcePath);
                    var destinationExists = PenMembership.Exists(intent.DestinationPath);
                    if (sourceExists && !destinationExists && NativeFileMove.Identity(intent.SourcePath) == intent.Identity)
                    {
                        // Restore membership at the verified original location; never replay a physical operation.
                        var sourcePaths = PenMembership.Normalize(source.ItemPaths.Append(intent.SourcePath));
                        var targetPaths = PenMembership.Remove(target.ItemPaths, intent.DestinationPath);
                        Save(intent.TargetPenId, target with { ItemPaths = targetPaths,
                            TrackedItems = ItemTracking.Normalize(targetPaths, target.TrackedItems) });
                        Save(intent.SourcePenId, source with { ItemPaths = sourcePaths,
                            TrackedItems = ItemTracking.Capture(sourcePaths, source.TrackedItems) });
                        WriteJournal(null);
                        return null;
                    }
                    if (sourceExists || !destinationExists || NativeFileMove.Identity(intent.DestinationPath) != intent.Identity)
                        throw new InvalidDataException("Ambiguous transfer filesystem state.");
                }
                var pair = Apply(intent, source, target);
                Save(intent.TargetPenId, pair.Target);
                Save(intent.SourcePenId, pair.Source);
                WriteJournal(null);
                return null;
            }
            catch (Exception exception) when (IsTransferError(exception))
            {
                Diagnostics.Write($"Pen transfer recovery failed: {exception.GetType().Name}");
                BlockedReason = "An interrupted Pen transfer needs review. Files and the transfer journal were retained; item operations are paused.";
                return BlockedReason;
            }
        }
    }

    private PenTransferResult Failure(PenSettings source, PenSettings target, string message, bool recovery = false)
    {
        if (recovery) BlockedReason = message;
        return new(source, target, false, recovery, message);
    }

    internal static (PenSettings Source, PenSettings Target) Apply(PenTransferIntent intent, PenSettings source, PenSettings target)
    {
        var sourcePaths = PenMembership.Remove(source.ItemPaths, intent.SourcePath);
        var targetPaths = PenMembership.Normalize(target.ItemPaths.Append(intent.DestinationPath));
        var targetTracking = target.TrackedItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(intent.Identity)) targetTracking = targetTracking.Append(new(intent.DestinationPath, intent.Identity));
        return (source with { ItemPaths = sourcePaths, TrackedItems = ItemTracking.Normalize(sourcePaths, source.TrackedItems) },
            target with { ItemPaths = targetPaths, TrackedItems = ItemTracking.Capture(targetPaths, targetTracking) });
    }

    private void ValidateIds(string source, string target)
    {
        var ids = workspace.Load().PenIds;
        if (!WorkspaceStore.IsValidId(source) || !WorkspaceStore.IsValidId(target)
            || string.Equals(source, target, StringComparison.OrdinalIgnoreCase)
            || !ids.Contains(source, StringComparer.OrdinalIgnoreCase) || !ids.Contains(target, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid transfer Pen identity.");
    }

    private void ValidateIntent(PenTransferIntent intent)
    {
        ValidateIds(intent.SourcePenId, intent.TargetPenId);
        if (intent.Id == Guid.Empty || string.IsNullOrWhiteSpace(intent.SourcePath) || string.IsNullOrWhiteSpace(intent.DestinationPath)
            || PenMembership.NormalizePath(intent.SourcePath) != intent.SourcePath
            || PenMembership.NormalizePath(intent.DestinationPath) != intent.DestinationPath)
            throw new InvalidDataException("Invalid transfer paths.");
        var source = new PenMoveService(desktop, intent.SourcePenId);
        var target = new PenMoveService(desktop, intent.TargetPenId);
        if (intent.Physical)
        {
            if (!source.IsManaged(intent.SourcePath) || !target.IsManaged(intent.DestinationPath)
                || string.IsNullOrWhiteSpace(intent.Identity)
                || !string.Equals(Path.GetFileName(intent.SourcePath), Path.GetFileName(intent.DestinationPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Transfer paths escape their Pen storage.");
        }
        else if (source.IsManaged(intent.SourcePath) || intent.SourcePath != intent.DestinationPath)
            throw new InvalidDataException("Invalid reference transfer.");
    }

    private PenSettings ReadStrict(string id)
    {
        var path = workspace.SettingsPath(id);
        if (!File.Exists(path)) throw new InvalidDataException("Missing Pen settings during recovery.");
        return (JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Invalid Pen settings during recovery.")).Normalize();
    }

    private void Save(string id, PenSettings settings)
    {
        if (!new PenSettingsStore(workspace.SettingsPath(id)).Save(settings))
            throw new IOException("The Pen settings snapshot could not be saved.");
    }

    private PenTransferIntent? ReadJournal()
    {
        if (!File.Exists(JournalPath)) return null;
        if (new FileInfo(JournalPath).LinkTarget is not null) throw new InvalidDataException("Transfer journal cannot be a link.");
        var text = File.ReadAllText(JournalPath);
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("Pending", out _)) throw new InvalidDataException("Missing transfer state.");
        var journal = JsonSerializer.Deserialize<PenTransferJournal>(text);
        if (journal is not { Version: 1 }) throw new InvalidDataException("Invalid transfer journal.");
        return journal.Pending;
    }

    private void WriteJournal(PenTransferIntent? pending) =>
        AtomicJsonFile.Write(JournalPath, new PenTransferJournal(1, pending));

    private static bool IsTransferError(Exception exception) => exception is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or Win32Exception or ArgumentException or NotSupportedException;
}
