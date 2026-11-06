using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop;

internal sealed record WorkspaceSettings(int Version, string[] PenIds, bool CreationEnabled = true, bool OrganizationOfferDismissed = false);

internal sealed class WorkspaceStore(string directory)
{
    internal string TransferJournalPath => Path.Combine(directory, "transfers.json");
    internal bool IsUninitialized => !File.Exists(IndexPath) && !File.Exists(SettingsPath("Default"))
        && (!Directory.Exists(Path.Combine(directory, "Pens")) || !Directory.EnumerateFileSystemEntries(Path.Combine(directory, "Pens")).Any());
    internal string SavedLayoutsPath => Path.Combine(directory, "saved-layouts.json");
    internal string FirstRunReportPath => Path.Combine(directory, "first-run-report.json");
    internal string RecycleBinStatePath => Path.Combine(directory, "recycle-bin-visibility.json");
    private string IndexPath => Path.Combine(directory, "workspace.json");

    internal static bool IsValidId(string? id) => id == "Default"
        || id is not null && Guid.TryParseExact(id, "N", out _);

    public string SettingsPath(string id)
    {
        if (!IsValidId(id)) throw new ArgumentException("Invalid Pen identity.", nameof(id));
        return id == "Default" ? Path.Combine(directory, "pen.json") : Path.Combine(directory, "Pens", id + ".json");
    }

    public WorkspaceSettings Load()
    {
        if (!File.Exists(IndexPath))
        {
            var migrated = new WorkspaceSettings(1, ["Default"]);
            Save(migrated);
            return migrated;
        }
        var settings = JsonSerializer.Deserialize<WorkspaceSettings>(File.ReadAllText(IndexPath));
        Validate(settings);
        return settings!;
    }

    public void Save(WorkspaceSettings settings)
    {
        Validate(settings);
        Directory.CreateDirectory(directory);
        AtomicJsonFile.Write(IndexPath, settings);
    }

    private static void Validate(WorkspaceSettings? settings)
    {
        if (settings is not { Version: 1, PenIds: not null }
            || settings.PenIds.Any(id => !IsValidId(id))
            || settings.PenIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.PenIds.Length)
            throw new InvalidDataException("The workspace index is invalid. Existing files were left untouched.");
    }
}
