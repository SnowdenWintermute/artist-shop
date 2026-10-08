namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

public static class CollectionOrder
{
    // when the artist's own drag order doesn't apply, such as the collections one work belongs to
    public static List<Collection> SortedByName(IEnumerable<Collection> collections) =>
        [.. collections.OrderBy(collection => collection.Name.Value, NameOrder.ByName)];

    // of these collection ids, the ones listed here, in the order they're listed, as a row shows its collections
    public static List<int> CollectionIdsInListOrder(IEnumerable<Collection> allCollections, IReadOnlySet<int> collectionIds) =>
        [.. allCollections.Select(collection => collection.Id.Value).Where(collectionIds.Contains)];
}
