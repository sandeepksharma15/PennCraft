namespace PennCraft.Desktop;

/// <summary>Empty includes hidden Pens, but never a live folder Portal or a saved item reference.</summary>
internal static class OrganizationOfferPolicy
{
    internal static bool IsEligible(IEnumerable<PenSettings> pens) =>
        pens.All(pen => pen.PortalFolder is null && !pen.IsVisible && pen.ItemPaths is { Length: 0 });
}
