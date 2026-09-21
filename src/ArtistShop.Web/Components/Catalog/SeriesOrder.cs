namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

// The order series are listed in when the artist's own drag order doesn't apply, such as the
// series one artwork belongs to
public static class SeriesOrder
{
    public static List<Series> SortedByName(IEnumerable<Series> series) =>
        [.. series.OrderBy(oneSeries => oneSeries.Name.Value, NameOrder.ByName)];
}
