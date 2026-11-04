namespace PennCraft.Desktop.Items;

internal static class ItemWatchPolicy
{
    public static bool IsRelevant(IReadOnlySet<string> members, string path, string? oldPath = null) =>
        members.Contains(path) || oldPath is not null && members.Contains(oldPath);
}
