namespace ArtistShop.Web.Sites;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ArtistShop.Web.Database;
using ArtistShop.Web.Domain.Sites;
using Npgsql;

// read from appsettings.json by ValidatedSettings. Postgres allows 100 connections in all, and each
// site keeps a pool of its own, so a site holds few, and lets them go soon after it goes quiet
public sealed record SiteDatabaseSettings
{
    [Range(1, 100)]
    public int MaximumPoolSize { get; init; }

    // at least Npgsql's 10 second pruning interval, which it refuses to start below
    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public TimeSpan ConnectionIdleLifetime { get; init; }
}

// Every site's own database, on the platform database's server and reached with its login. One
// data source per site, made the first time the site is asked for and then kept, since a data
// source holds that site's pool of connections
public sealed class SiteDatabases(
    string platformConnectionString,
    string databaseNamePrefix,
    SiteDatabaseSettings settings
) : IAsyncDisposable
{
    // the app's site databases, artist_shop_site_1 and on. The tests use a prefix of their own, since
    // they run against the same server as dev
    public const string DatabaseNamePrefix = "artist_shop_site_";

    // Lazy, since GetOrAdd can run its factory twice when two requests for a site race, and a data
    // source made and then dropped would never be disposed
    private readonly ConcurrentDictionary<SiteId, Lazy<NpgsqlDataSource>> _dataSources = new();

    public string ConnectionString(SiteId siteId) =>
        new NpgsqlConnectionStringBuilder(platformConnectionString)
        {
            Database = databaseNamePrefix + siteId.Value.ToString(CultureInfo.InvariantCulture),
            MaxPoolSize = settings.MaximumPoolSize,
            // in seconds, as Npgsql counts it
            ConnectionIdleLifetime = (int)settings.ConnectionIdleLifetime.TotalSeconds,
        }.ConnectionString;

    public NpgsqlDataSource For(SiteId siteId) =>
        _dataSources
            .GetOrAdd(siteId, id => new Lazy<NpgsqlDataSource>(() => SiteDataSource.Create(ConnectionString(id))))
            .Value;

    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in _dataSources.Values.Where(dataSource => dataSource.IsValueCreated))
        {
            await dataSource.Value.DisposeAsync();
        }
    }
}
