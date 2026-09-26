namespace ArtistShop.Web.Database;

using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// Makes, brings up to date and drops sites' schemas in the platform database
public sealed class SiteSchemas(string platformConnectionString)
{
    // Made if it isn't there, then its migrations and functions. Safe to run again
    public void Migrate(SiteId siteId)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(platformConnectionString)
        {
            // where the migrations make their tables and functions, and find the shared types
            SearchPath = SiteSchema.SearchPath(siteId),
            // Npgsql keeps a pool per connection string, and each site's is different, so at startup
            // every site would leave a pool of idle connections behind
            Pooling = false,
        }.ConnectionString;

        using (var connection = new NpgsqlConnection(connectionString))
        {
            // a schema name is an identifier, not a value, so it can't be a parameter; this one is
            // made from a number
            connection.Execute($"CREATE SCHEMA IF NOT EXISTS {SiteSchema.Name(siteId)}");
        }

        SchemaMigrator.Site.Upgrade(connectionString, SiteSchema.Name(siteId));
    }

    // every site schema there is, whether or not its site is listed
    public async Task<List<SiteId>> GetSiteIdsAsync()
    {
        await using var connection = new NpgsqlConnection(platformConnectionString);

        var names = await connection.QueryAsync<string>("SELECT schema_name FROM information_schema.schemata");

        return [.. names.Select(SiteSchema.IdFromName).OfType<SiteId>()];
    }

    // the schema and everything in it
    public async Task DropAsync(SiteId siteId)
    {
        await using var connection = new NpgsqlConnection(platformConnectionString);

        await connection.ExecuteAsync($"DROP SCHEMA IF EXISTS {SiteSchema.Name(siteId)} CASCADE");
    }
}
