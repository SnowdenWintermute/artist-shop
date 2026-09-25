namespace ArtistShop.Web.Search;

using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class SqlArtworkTitleSearch(SiteDatabase database) : ArtworkSearch
{
    public override async Task<IReadOnlyList<ArtworkId>> FindAsync(string searchText)
    {
        // no stored title is this long, so nothing can contain a longer text
        if (searchText.Length > ArtistShopLimits.ArtworkNameMaximumLength)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync();

        var ids = await connection.QueryAsync<int>(
            "SELECT * FROM find_artwork_ids_by_name(@Search)",
            new { Search = searchText }
        );

        return [.. ids.Select(id => new ArtworkId(id))];
    }
}
