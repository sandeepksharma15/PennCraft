using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PennCraft.Desktop.Infrastructure;
using PennCraft.Desktop.Items;

namespace PennCraft.Desktop;

internal sealed record LayoutGeometry([property: JsonRequired] double Left, [property: JsonRequired] double Top,
    [property: JsonRequired] double Width, [property: JsonRequired] double Height);

internal sealed record LayoutJournal([property: JsonRequired] int Version,
    [property: JsonRequired] Dictionary<string, LayoutGeometry>? Before);

/// <summary>All settings commit together or a pending intent restores the previous snapshots on startup.</summary>
internal sealed class LayoutTransaction(WorkspaceStore workspace)
{
    internal string JournalPath => workspace.TransferJournalPath + ".layout";
    internal string? BlockedReason { get; private set; }

    internal string? Recover()
    {
        lock (PenMoveService.FileOperationGate)
        {
            try
            {
                var before = Read();
                if (before is null) return null;
                Validate(before);
                Save(before);
                Write(null);
                return null;
            }
            catch (Exception exception) when (Expected(exception))
            {
                return BlockedReason = "A saved layout could not be recovered. Its record was retained; restart after checking settings access.";
            }
        }
    }

    internal bool Execute(Dictionary<string, PenSettings> before,
        Func<Dictionary<string, PenSettings>> apply, Action rollback)
    {
        lock (PenMoveService.FileOperationGate)
        {
            var prepared = false;
            var journalChecked = false;
            try
            {
                if (BlockedReason is not null) return false;
                if (Read() is not null)
                {
                    BlockedReason = "A previous layout needs startup recovery.";
                    return false;
                }
                journalChecked = true;
                Validate(before);
                Save(before); // Preflight before moving any window.
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
                    BlockedReason = "The saved layout record could not be read. Restart after checking settings access.";
                if (prepared)
                {
                    try { rollback(); Save(before); Write(null); }
                    catch (Exception recoveryError) when (Expected(recoveryError))
                    {
                        BlockedReason = "The layout could not be saved or fully restored. Restart PennCraft to recover the previous layout.";
                    }
                }
                return false;
            }
        }
    }

    private Dictionary<string, PenSettings>? Read()
    {
        if (!File.Exists(JournalPath)) return null;
        if (new FileInfo(JournalPath).LinkTarget is not null) throw new InvalidDataException("Linked layout record.");
        var journal = JsonSerializer.Deserialize<LayoutJournal>(File.ReadAllText(JournalPath));
        if (journal is not { Version: 1 }) throw new InvalidDataException("Invalid layout record.");
        if (journal.Before is null) return null;
        if (journal.Before.Any(pair => pair.Value is null)) throw new InvalidDataException("Missing layout geometry.");
        return journal.Before.ToDictionary(pair => pair.Key, pair => new PenSettings
        {
            Left = pair.Value.Left, Top = pair.Value.Top, Width = pair.Value.Width, Height = pair.Value.Height
        });
    }

    private void Validate(Dictionary<string, PenSettings> snapshots)
    {
        var ids = workspace.Load().PenIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (snapshots.Count == 0 || snapshots.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshots.Count
            || snapshots.Any(pair => !WorkspaceStore.IsValidId(pair.Key) || !ids.Contains(pair.Key) || pair.Value is null
                || !double.IsFinite(pair.Value.Left) || !double.IsFinite(pair.Value.Top)
                || !double.IsFinite(pair.Value.Width) || !double.IsFinite(pair.Value.Height)
                || pair.Value.Width < PenSettings.MinimumWidth || pair.Value.Height < PenSettings.MinimumHeight))
            throw new InvalidDataException("Invalid layout participants.");
    }

    private void Save(Dictionary<string, PenSettings> snapshots)
    {
        foreach (var (id, geometry) in snapshots)
        {
            var current = JsonSerializer.Deserialize<PenSettings>(File.ReadAllText(workspace.SettingsPath(id)))
                ?? throw new InvalidDataException("Missing settings during layout.");
            var next = current with { Left = geometry.Left, Top = geometry.Top, Width = geometry.Width, Height = geometry.Height };
            if (!new PenSettingsStore(workspace.SettingsPath(id)).Save(next))
                throw new IOException("Layout settings could not be saved.");
        }
    }

    private void Write(Dictionary<string, PenSettings>? before) =>
        AtomicJsonFile.Write(JournalPath, new LayoutJournal(1, before?.ToDictionary(pair => pair.Key,
            pair => new LayoutGeometry(pair.Value.Left, pair.Value.Top, pair.Value.Width, pair.Value.Height))));

    private static bool Expected(Exception exception) => exception is IOException or InvalidDataException
        or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException
        or System.ComponentModel.Win32Exception or NotSupportedException;
}
