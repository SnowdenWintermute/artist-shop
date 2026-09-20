namespace ArtistShop.Web.Search;

using System.Data;
using ArtistShop.Web.Database;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using Dapper;

public class SqlArtworkTitleSearch(SqlConnectionFactory connectionFactory) : ArtworkSearch
{
    public override async Task<IReadOnlyList<ArtworkId>> FindAsync(string searchText)
    {
        // no stored title is this long, so nothing can contain a longer text. Without this
        // the procedure's nvarchar(200) would silently truncate and match the wrong artworks
        if (searchText.Length > CatalogLimits.ArtworkNameMaximumLength)
        {
            return [];
        }

        await using var connection = connectionFactory.Create();

        var ids = await connection.QueryAsync<int>(
            "dbo.FindArtworkIdsByName",
            new { Search = searchText },
            commandType: CommandType.StoredProcedure
        );

        return [.. ids.Select(id => new ArtworkId(id))];
    }
}
