namespace PennCraft.Desktop;

/// <summary>Hidden Portals are preserved; visible containers or saved Pen references suppress the offer.</summary>
internal static class OrganizationOfferPolicy
{
    internal static bool IsEligible(IEnumerable<PenSettings> pens) =>
        pens.All(pen => !pen.IsVisible && (pen.PortalFolder is not null || pen.ItemPaths is { Length: 0 }));
}
