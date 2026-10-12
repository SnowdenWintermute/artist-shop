namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

public class NavEntryRepository(SiteDatabase database)
{
    // the pages in the nav, in the artist's order
    public async Task<List<CustomPage>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CustomPageRepository.CustomPageRow>("SELECT * FROM get_all_nav_entries()");

        return [.. rows.Select(row => row.ToPage())];
    }

    // replaces the whole nav. A page deleted since the list was loaded is refused
    public async Task SetAsync(IReadOnlyList<CustomPageId> pageIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_nav_entries(@PageIds)",
                new { PageIds = pageIds.Select(id => id.Value).ToArray() }
            );
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ChangedSincePageLoadException("A page in the nav no longer exists.", exception);
        }
    }
}
