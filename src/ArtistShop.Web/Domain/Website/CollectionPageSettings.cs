namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Domain.Catalog;

// What the artist chooses about the fixed page each collection has. The rest of its grid is fixed
public sealed record CollectionPageSettings(CardGridLayout Layout, int CardsPerPage)
{
    // headed by the page's own h1, and always with page links, since nothing else leads to the rest
    public CardGridSettings GridFor(CollectionId collectionId) =>
        new(new CardGridSource.WorksInCollection(collectionId), new CardGridHeading.None(), CardsPerPage,
            HasPageLinks: true, Layout);
}
