namespace ArtistShop.Web.Search;

using ArtistShop.Web.Domain.Catalog;

// The list page asks which artworks a search text matches and knows nothing else about
// searching, so a full text index or a search service can take this over without touching it
public abstract class ArtworkSearch
{
    public abstract Task<IReadOnlyList<ArtworkId>> FindAsync(string searchText);
}
