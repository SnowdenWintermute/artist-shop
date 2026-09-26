using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Dapper;

namespace ArtistShop.Web.Tests.Sites;

// A sign-up whose site couldn't be added leaves nothing behind
[Collection(DatabaseCollection.Name)]
public sealed class SiteProvisionerTests : IDisposable
{
    private readonly TestDatabaseFixture _database;
    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
    private readonly ImageStorageSettings _storageSettings;
    private readonly SiteProvisioner _provisioner;

    public SiteProvisionerTests(TestDatabaseFixture database)
    {
        _database = database;
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _provisioner = new SiteProvisioner(
            new SiteRepository(database.PlatformDataSource),
            new SiteSchemas(database.PlatformConnectionString),
            _storageSettings
        );
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    [Fact]
    public async Task ASiteThatCouldntBeAddedLeavesNoSchemaOrFolders()
    {
        SiteId? made = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provisioner.CreateAsync(siteId =>
            {
                made = siteId;
                throw new InvalidOperationException("The site couldn't be added.");
            })
        );

        var siteId = Assert.IsType<SiteId>(made);
        Assert.DoesNotContain(siteId, await new SiteSchemas(_database.PlatformConnectionString).GetSiteIdsAsync());
        Assert.DoesNotContain(siteId, ImageStorage.SiteIdsWithFolders(_storageSettings));
    }

    [Fact]
    public void ReadsASitesIdFromItsSchemaNameOnly()
    {
        Assert.Equal(new SiteId(12), SiteSchema.IdFromName("site_12"));
        Assert.Null(SiteSchema.IdFromName(SiteSchema.TypesSchema));
        Assert.Null(SiteSchema.IdFromName("public"));
    }
}
