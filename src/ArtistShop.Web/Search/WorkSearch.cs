namespace ArtistShop.Web.Search;

using ArtistShop.Web.Domain.Catalog;

// The list page asks which works a search text matches and knows nothing else about
// searching, so a full text index or a search service can take this over without touching it
public abstract class WorkSearch
{
    public abstract Task<IReadOnlyList<WorkId>> FindAsync(string searchText);
}
