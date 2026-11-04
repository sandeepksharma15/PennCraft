using System.IO;
using System.Text.Json;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop;

internal sealed record WorkspaceSettings(int Version, string[] PenIds, bool CreationEnabled = true);

internal sealed class WorkspaceStore(string directory)
{
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
