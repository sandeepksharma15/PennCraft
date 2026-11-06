using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PennCraft.Desktop.Infrastructure;

namespace PennCraft.Desktop;

internal sealed record SavedLayoutLibrary([property: JsonRequired] int Version, [property: JsonRequired] SavedLayout[] Layouts);

/// <summary>Atomic named snapshot catalog; unreadable records are retained rather than replaced.</summary>
internal sealed class SavedLayoutStore(string filePath)
{
    internal SavedLayout[] Load()
    {
        if (!File.Exists(filePath)) return [];
        if (new FileInfo(filePath).LinkTarget is not null) throw new InvalidDataException("Linked layout catalog.");
        var library = JsonSerializer.Deserialize<SavedLayoutLibrary>(File.ReadAllText(filePath));
        if (library is not { Version: 1, Layouts: not null } || library.Layouts.Length > 100)
            throw new InvalidDataException("The saved layout catalog is invalid.");
        foreach (var layout in library.Layouts) Validate(layout);
        if (library.Layouts.Select(layout => layout.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != library.Layouts.Length
            || library.Layouts.Select(layout => layout.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != library.Layouts.Length)
            throw new InvalidDataException("The saved layout catalog contains duplicate entries.");
        return library.Layouts;
    }

    internal static string? NameError(string name) => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80
        || name.Any(char.IsControl) ? "Enter a name of 1–80 characters without control characters." : null;

    internal SavedLayout Add(string name, string stamp, Dictionary<string, LayoutPresentation> pens)
    {
        var layouts = Load();
        CheckName(name, layouts, null);
        if (layouts.Length >= 100) throw new InvalidDataException("Delete a saved layout before adding another (limit: 100).");
        var layout = new SavedLayout(Guid.NewGuid().ToString("N"), name.Trim(), stamp, pens);
        Validate(layout);
        Write([.. layouts, layout]);
        return layout;
    }

    internal void Rename(string id, string name)
    {
        var layouts = Load();
        if (!layouts.Any(layout => layout.Id == id)) throw new InvalidDataException("This saved layout no longer exists.");
        CheckName(name, layouts, id);
        Write(layouts.Select(layout => layout.Id == id ? layout with { Name = name.Trim() } : layout).ToArray());
    }

    internal void Delete(string id)
    {
        var layouts = Load();
        if (!layouts.Any(layout => layout.Id == id)) throw new InvalidDataException("This saved layout no longer exists.");
        Write(layouts.Where(layout => layout.Id != id).ToArray());
    }

    private static void CheckName(string name, SavedLayout[] layouts, string? id)
    {
        if (NameError(name) is { } error) throw new InvalidDataException(error);
        if (layouts.Any(layout => layout.Id != id && string.Equals(layout.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("A layout already uses that name. Choose another name.");
    }

    internal static void Validate(SavedLayout? layout)
    {
        if (layout is null || !Guid.TryParseExact(layout.Id, "N", out _) || layout.Name is null
            || NameError(layout.Name) is not null || layout.Name != layout.Name.Trim()
            || string.IsNullOrWhiteSpace(layout.DisplayStamp) || layout.DisplayStamp.Length > 16384
            || layout.Pens is null || layout.Pens.Count == 0
            || layout.Pens.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != layout.Pens.Count
            || layout.Pens.Any(pair => !WorkspaceStore.IsValidId(pair.Key) || pair.Value is null))
            throw new InvalidDataException("A saved layout is invalid.");
        foreach (var value in layout.Pens.Values) value.Validate();
    }

    private void Write(SavedLayout[] layouts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        AtomicJsonFile.Write(filePath, new SavedLayoutLibrary(1, layouts));
    }
}
