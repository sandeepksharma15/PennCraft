using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop.Items;

internal sealed record MoveEntry(Guid Id, string Source, string Destination, string Identity, bool IntoPen);
internal sealed record MoveJournal(int Version, MoveEntry[] Entries);

/// <summary>Persist intent before rename, then acknowledge only after settings are durable.</summary>
internal static class MoveJournalFile
{
    public static MoveEntry[] Read(string path)
    {
        if (!File.Exists(path)) return [];
        var journal = JsonSerializer.Deserialize<MoveJournal>(File.ReadAllText(path));
        if (journal is not { Version: 1, Entries: not null })
            throw new InvalidDataException("Unsupported or malformed move journal.");
        return journal.Entries;
    }

    public static void Write(string path, MoveEntry[] entries) =>
        AtomicJsonFile.Write(path, new MoveJournal(1, entries));
}
