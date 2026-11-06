using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

internal sealed record PresentationJournal([property: JsonRequired] int Version,
    [property: JsonRequired] Dictionary<string, LayoutPresentation>? Before);

/// <summary>Presentation-only recovery, independent from geometry-only automatic layout journals.</summary>
internal sealed class SavedLayoutTransaction(WorkspaceStore workspace)
{
    internal string JournalPath => workspace.TransferJournalPath + ".presentation";
    internal string? BlockedReason { get; private set; }

    internal string? Recover()
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                var before = Read();
                if (before is null) return null;
                journalChecked = true;
                Validate(before);
                Save(before);
                Write(null);
                return null;
            }
            catch (Exception exception) when (Expected(exception))
            {
                return BlockedReason = "A layout restore needs recovery. Its record was retained; restart after checking settings access.";
            }
        }
    }

    internal bool Execute(Dictionary<string, LayoutPresentation> before,
        Func<Dictionary<string, LayoutPresentation>> apply, Action rollback)
    {
        lock (PenMoveService.FileOperationGate)
        {
            var prepared = false;
            var journalChecked = false;
            try
            {
                if (BlockedReason is not null) return false;
                if (Read() is not null) throw new InvalidDataException("A restore needs recovery.");
                Validate(before);
                Save(before);
                Write(before);
                prepared = true;
                var after = apply();
                Validate(after);
                if (!before.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(after.Keys))
                    throw new InvalidDataException("Layout participants changed.");
                Save(after);
                Write(null);
                return true;
            }
            catch (Exception exception) when (Expected(exception))
            {
                if (!prepared && !journalChecked && File.Exists(JournalPath))
                    BlockedReason = "A previous layout restore record needs startup recovery.";
                if (prepared)
                {
                    try { rollback(); Save(before); Write(null); }
                    catch (Exception recoveryError) when (Expected(recoveryError))
                    {
                        BlockedReason = "The previous presentation could not be fully restored. Restart PennCraft to recover it.";
                    }
                }
                return false;
            }
        }
    }

    private Dictionary<string, LayoutPresentation>? Read()
    {
        if (!File.Exists(JournalPath)) return null;
        if (new FileInfo(JournalPath).LinkTarget is not null) throw new InvalidDataException("Linked presentation record.");
        var journal = JsonSerializer.Deserialize<PresentationJournal>(File.ReadAllText(JournalPath));
        if (journal is not { Version: 1 }) throw new InvalidDataException("Invalid presentation record.");
        return journal.Before;
    }

    private void Validate(Dictionary<string, LayoutPresentation> snapshots)
    {
        var ids = workspace.Load().PenIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (snapshots.Count == 0 || snapshots.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Count
            || snapshots.Any(pair => !WorkspaceStore.IsValidId(pair.Key) || !ids.Contains(pair.Key) || pair.Value is null))
            throw new InvalidDataException("Invalid restore participants.");
        foreach (var value in snapshots.Values) value.Validate();
    }

    private void Save(Dictionary<string, LayoutPresentation> snapshots)
    {
        foreach (var (id, presentation) in snapshots)
        {
            var current = JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(workspace.SettingsPath(id)))
                ?? throw new InvalidDataException("Missing settings during layout restore.");
            if (!new PenSettingsStore(workspace.SettingsPath(id)).Save(presentation.Merge(current)))
                throw new IOException("Layout presentation could not be saved.");
        }
    }

    private void Write(Dictionary<string, LayoutPresentation>? before) =>
        AtomicJsonFile.Write(JournalPath, new PresentationJournal(1, before));

    internal static bool Expected(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception
        or NotSupportedException;
}
