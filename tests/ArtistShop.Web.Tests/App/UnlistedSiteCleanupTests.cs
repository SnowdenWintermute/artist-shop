using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// What startup does before serving: a sign-up that crashed after making its site's schema and
// folders, but before adding the site, leaves them unlisted. In the app's own databases, since the
// repository tests share a schema with no site row
[Collection(TestAppCollection.Name)]
public sealed class UnlistedSiteCleanupTests(TestApp app)
{
    [Fact]
    public async Task RemovesAnUnlistedSitesSchemaAndFoldersAndKeepsListedOnes()
    {
        var schemas = app.Services.GetRequiredService<SiteSchemas>();
        var storageSettings = app.Services.GetRequiredService<ImageStorageSettings>();
        var unlisted = await app.Services.GetRequiredService<SiteRepository>().ReserveIdAsync();
        schemas.Migrate(unlisted);
        ImageStorage.ForSite(storageSettings, unlisted).CreateFolders();

        await app.Services.GetRequiredService<SiteProvisioner>().RemoveUnlistedAsync();

        Assert.DoesNotContain(unlisted, await schemas.GetSiteIdsAsync());
        Assert.DoesNotContain(unlisted, ImageStorage.SiteIdsWithFolders(storageSettings));
        Assert.Contains(app.FirstSiteId, await schemas.GetSiteIdsAsync());
        Assert.Contains(app.FirstSiteId, ImageStorage.SiteIdsWithFolders(storageSettings));
    }
}
