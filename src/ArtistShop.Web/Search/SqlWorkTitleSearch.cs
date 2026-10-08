namespace ArtistShop.Web.Search;

using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class SqlWorkTitleSearch(SiteDatabase database) : WorkSearch
{
    public override async Task<IReadOnlyList<WorkId>> FindAsync(string searchText)
    {
        // no stored title is this long, so nothing can contain a longer text
        if (searchText.Length > ArtistShopLimits.WorkNameMaximumLength)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync();

        var ids = await connection.QueryAsync<int>(
            "SELECT * FROM find_work_ids_by_name(@Search)",
            new { Search = searchText }
        );

        return [.. ids.Select(id => new WorkId(id))];
    }
}
