namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;

using ArtistShop.Web.Domain.Catalog;

// The artworks checked in an ArtworkListBrowser, kept in the address as one FieldName parameter per
// artwork, so they stay checked while the filters and the page change
public record ArtworkListSelection(string FieldName, IReadOnlyList<ArtworkId> Ids)
{
    public bool Contains(ArtworkId id) => Ids.Contains(id);
}
