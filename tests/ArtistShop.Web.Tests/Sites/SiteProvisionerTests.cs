using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Dapper;
using Npgsql;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteProvisionerTests : IAsyncDisposable
{
    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
    private readonly ImageStorageSettings _storageSettings;
    private readonly SiteDatabases _siteDatabases;
    private readonly SiteRepository _sites;
    private readonly SiteProvisioner _provisioner;
    private readonly NpgsqlDataSource _platformDataSource;
    private readonly string _platformConnectionString;

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    public SiteProvisionerTests(TestDatabaseFixture database)
    {
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _siteDatabases = database.SiteDatabases;
        _sites = new SiteRepository(database.PlatformDataSource);
        _provisioner = new SiteProvisioner(_sites, new SiteSchemas(database.PlatformConnectionString), _storageSettings);
        _platformDataSource = database.PlatformDataSource;
        _platformConnectionString = database.PlatformConnectionString;
    }

    public ValueTask DisposeAsync()
    {
        _storageRoot.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ANewSiteHasItsOwnSchemaAndFolders()
    {
        var siteId = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        // an empty, up-to-date site schema: its functions are there to be called
        Assert.Empty(await new PostRepository(_siteDatabases.For(siteId)).GetAllAsync());

        var storage = ImageStorage.ForSite(_storageSettings, siteId);
        Assert.True(Directory.Exists(storage.Originals));
        Assert.True(Directory.Exists(storage.Variants));
    }

    // one site's post isn't in another's schema
    [Fact]
    public async Task SitesKeepTheirDataApart()
    {
        var first = await _provisioner.CreateAsync([NewHost()], OwnerUserId);
        var second = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        await new PostRepository(_siteDatabases.For(first)).AddAsync(
            new PostTitle("Only on the first site"),
            PostSlug.FromTitle("Only on the first site"),
            new PostBody("""{"ops":[{"insert":"Hello\n"}]}"""),
            PostStatus.Published
        );

        Assert.Single(await new PostRepository(_siteDatabases.For(first)).GetAllAsync());
        Assert.Empty(await new PostRepository(_siteDatabases.For(second)).GetAllAsync());
    }

    [Fact]
    public async Task PreparingASiteAgainChangesNothing()
    {
        var siteId = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        _provisioner.Prepare(siteId);

        Assert.Empty(await new PostRepository(_siteDatabases.For(siteId)).GetAllAsync());
    }

    // The row is added last, so a site that fails to be added leaves nothing behind: no row, and
    // its schema dropped
    [Fact]
    public async Task ASiteThatCantBeAddedLeavesNoSchema()
    {
        var host = NewHost();
        await _provisioner.CreateAsync([host], OwnerUserId);
        SiteId? attempted = null;

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _provisioner.CreateAsync(siteId =>
            {
                attempted = siteId;
                return _sites.AddAsync(siteId, [host], OwnerUserId);
            })
        );

        var siteId = attempted ?? throw new InvalidOperationException("The site was never tried.");
        Assert.DoesNotContain(siteId, await _sites.GetIdsAsync());
        Assert.False(await SchemaExistsAsync(siteId));
    }

    // A connection that comes back to the shared pool from one site's use, then is taken without
    // naming a site, finds no site's functions, rather than that site's
    [Fact]
    public async Task AConnectionOpenedWithoutASiteFindsNoSitesFunctions()
    {
        // a pool of one, so the second connection is sure to be the one the site used
        await using var sitesDataSource = SiteDataSource.Create(
            _platformConnectionString,
            new SiteDatabaseSettings { MaximumPoolSize = 1, ConnectionIdleLifetime = TimeSpan.FromSeconds(10) }
        );
        var siteId = await _provisioner.CreateAsync([NewHost()], OwnerUserId);
        Assert.Empty(await new PostRepository(new SiteDatabases(sitesDataSource).For(siteId)).GetAllAsync());

        await using var connection = await sitesDataSource.OpenConnectionAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.QueryAsync("SELECT * FROM get_post_list()")
        );
        Assert.Equal(PostgresErrorCodes.UndefinedFunction, exception.SqlState);
    }

    private async Task<bool> SchemaExistsAsync(SiteId siteId)
    {
        await using var connection = _platformDataSource.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT FROM pg_namespace WHERE nspname = @Name)",
            new { Name = SiteSchema.Name(siteId) }
        );
    }

    private static HostName NewHost() =>
        HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
}
