namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

public static class SeriesOrder
{
    // when the artist's own drag order doesn't apply, such as the series one artwork belongs to
    public static List<Series> SortedByName(IEnumerable<Series> series) =>
        [.. series.OrderBy(oneSeries => oneSeries.Name.Value, NameOrder.ByName)];

    // of these series ids, the ones listed here, in the order they're listed, as a row shows its series
    public static List<int> SeriesIdsInListOrder(IEnumerable<Series> allSeries, IReadOnlySet<int> seriesIds) =>
        [.. allSeries.Select(series => series.Id.Value).Where(seriesIds.Contains)];
}
