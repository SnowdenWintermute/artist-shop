namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

public static class ArtworkTypeOrder
{
    public static List<ArtworkType> SortedByName(IEnumerable<ArtworkType> artworkTypes) =>
        [.. artworkTypes.OrderBy(artworkType => artworkType.Name.Value, NameOrder.ByName)];
}
