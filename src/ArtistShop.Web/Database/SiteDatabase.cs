namespace ArtistShop.Web.Database;

using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// One site's schema, reached through the pool every site shares (SiteDataSource). Each connection
// it opens has search_path set to the site's schema, so the site's functions are found by their
// bare names. The pool wipes session settings as a connection comes back, and the pool's own
// search_path is only site_types, so a connection opened any other way finds no site's functions
public sealed class SiteDatabase(NpgsqlDataSource sitesDataSource, SiteId siteId)
{
    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = await sitesDataSource.OpenConnectionAsync();

        try
        {
            // set_config rather than SET, which can't take the value as a parameter
            await connection.ExecuteAsync(
                "SELECT set_config('search_path', @SearchPath, false)",
                new { SearchPath = SiteSchema.SearchPath(siteId) }
            );
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }
}
