namespace PennCraft.Desktop.Items;

internal sealed class PenDragSession(string sourcePenId, string path, string? identity)
{
    public string Token { get; } = Guid.NewGuid().ToString("N");
    public string SourcePenId { get; } = sourcePenId;
    public string Path { get; } = path;
    public string? Identity { get; } = identity;
    public bool Accepted { get; set; }
}

/// <summary>Only the current in-process drag resolves its opaque token. External data never supplies paths.</summary>
internal sealed class PenDragRegistry
{
    internal const string Format = "PennCraft.PenTransfer.v1";
    private PenDragSession? active;

    public PenDragSession Begin(string sourcePenId, string path, string? identity)
    {
        if (active is not null) throw new InvalidOperationException("A Pen drag is already active.");
        if (!WorkspaceStore.IsValidId(sourcePenId) || string.IsNullOrWhiteSpace(path) || PenMembership.NormalizePath(path) != path)
            throw new ArgumentException("Invalid drag source.");
        return active = new(sourcePenId, path, identity);
    }

    public PenDragSession? Resolve(string? token) => active is { Accepted: false } current
        && string.Equals(current.Token, token, StringComparison.Ordinal) ? current : null;

    public void End(PenDragSession session) { if (ReferenceEquals(active, session)) active = null; }
}
