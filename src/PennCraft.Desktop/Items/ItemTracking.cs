using System.ComponentModel;
using System.Globalization;
using System.IO;
using PennCraft.Desktop.Windows;

namespace PennCraft.Desktop.Items;

public sealed record TrackedItem(string Path, string Identity);
internal sealed record ItemReconciliation(string[] Paths, TrackedItem[] Tracking, bool Uncertain);

/// <summary>Metadata-only reconciliation within original parent folders; never moves or deletes an item.</summary>
internal static class ItemTracking
{
    public static TrackedItem[] Normalize(IEnumerable<string> paths, IEnumerable<TrackedItem>? tracking)
    {
        var members = PenMembership.Normalize(paths).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, TrackedItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in tracking ?? [])
        {
            if (entry is null) continue;
            var path = PenMembership.NormalizePath(entry.Path);
            if (path is not null && members.Contains(path) && ValidIdentity(entry.Identity))
                result.TryAdd(path, entry with { Path = path });
        }
        return [.. result.Values];
    }

    private static bool ValidIdentity(string? identity)
    {
        var parts = identity?.Split(':');
        return parts is { Length: 3 } && parts[0].Length == 8 && parts[1].Length == 16 && parts[2].Length == 16
            && uint.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            && ulong.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            && ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }

    public static TrackedItem[] Capture(IEnumerable<string> paths, IEnumerable<TrackedItem> previous)
    {
        var members = PenMembership.Normalize(paths);
        var known = Normalize(members, previous).ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var path in members)
        {
            if (known.ContainsKey(path)) continue; // Never adopt a replacement before reconciling the old identity.
            var identity = TryIdentity(path);
            if (identity is not null) known[path] = new(path, identity);
        }
        return [.. known.Values];
    }

    public static ItemReconciliation Reconcile(IEnumerable<string> paths, IEnumerable<TrackedItem> previous)
    {
        var members = PenMembership.Normalize(paths);
        var known = Normalize(members, previous).ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        var parents = new Dictionary<string, ParentSnapshot>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var tracking = new List<TrackedItem>();
        var uncertain = false;
        foreach (var path in members)
        {
            if (!known.TryGetValue(path, out var entry)) { result.Add(path); continue; }
            if (TryIdentity(path) == entry.Identity)
            {
                result.Add(path);
                tracking.Add(entry);
                continue;
            }
            var parent = System.IO.Path.GetDirectoryName(path)!;
            if (!parents.TryGetValue(parent, out var scan)) parents[parent] = scan = Scan(parent);
            var matches = scan.Items.Where(item => item.Identity == entry.Identity).ToArray();
            if (scan.Complete && matches.Length == 1)
            {
                result.Add(matches[0].Path);
                tracking.Add(matches[0]);
            }
            else if (!scan.Complete || matches.Length > 1)
            {
                result.Add(path);
                tracking.Add(entry);
                uncertain = true;
            }
            // A complete scan with no matching identity confirms that the original left this folder.
        }
        var normalized = PenMembership.Normalize(result);
        return new(normalized, Normalize(normalized, tracking), uncertain);
    }

    private static ParentSnapshot Scan(string parent)
    {
        var items = new List<TrackedItem>();
        var complete = true;
        try
        {
            if (!Directory.Exists(parent)) return new([], false);
            foreach (var path in Directory.EnumerateFileSystemEntries(parent))
            {
                var identity = TryIdentity(path);
                if (identity is null) complete = false;
                else items.Add(new(path, identity));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            complete = false;
        }
        return new([.. items], complete);
    }

    private static string? TryIdentity(string path)
    {
        try { return NativeFileMove.Identity(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception
            or ArgumentException or NotSupportedException) { return null; }
    }

    private sealed record ParentSnapshot(TrackedItem[] Items, bool Complete);
}
