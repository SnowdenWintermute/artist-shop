namespace ArtistShop.Web.Sites;

using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Database;
using ArtistShop.Web.Domain.Sites;
using Npgsql;

// read from appsettings.json by ValidatedSettings, for the one pool every site shares. Postgres
// allows 100 connections in all, and the platform and Identity have pools too
public sealed record SiteDatabaseSettings
{
    [Range(1, 100)]
    public int MaximumPoolSize { get; init; }

    // at least Npgsql's 10 second pruning interval, which it refuses to start below
    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public TimeSpan ConnectionIdleLifetime { get; init; }
}

// Every site's schema, reached through the shared pool SiteDataSource makes
public sealed class SiteDatabases(NpgsqlDataSource sitesDataSource)
{
    public SiteDatabase For(SiteId siteId) => new(sitesDataSource, siteId);
}
