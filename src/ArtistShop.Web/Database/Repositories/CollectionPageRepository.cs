namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;

public class CollectionPageRepository(SiteDatabase database)
{
    public async Task<CollectionPageSettings> GetAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<CollectionPageRow>("SELECT * FROM get_collection_page()");

        return new CollectionPageSettings(row.Layout, row.CardsPerPage);
    }

    public async Task UpdateAsync(CollectionPageSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT update_collection_page(@Layout, @CardsPerPage)",
            new { Layout = (int)settings.Layout, settings.CardsPerPage }
        );
    }

    private sealed class CollectionPageRow
    {
        public required CardGridLayout Layout { get; init; }
        public required int CardsPerPage { get; init; }
    }
}
