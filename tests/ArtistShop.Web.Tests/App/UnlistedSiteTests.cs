using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// What startup reports: a sign-up that crashed after making its site's schema and folders, but
// before adding the site, leaves them unlisted. In the app's own databases, since the repository
// tests share a schema with no site row
[Collection(TestAppCollection.Name)]
public sealed class UnlistedSiteTests(TestApp app)
{
    [Fact]
    public async Task FindsAnUnlistedSiteWithoutRemovingItAndSkipsListedOnes()
    {
        var schemas = app.Services.GetRequiredService<SiteSchemas>();
        var storageSettings = app.Services.GetRequiredService<ImageStorageSettings>();
        var unlisted = await app.Services.GetRequiredService<SiteRepository>().ReserveIdAsync();
        schemas.Migrate(unlisted);
        ImageStorage.ForSite(storageSettings, unlisted).CreateFolders();

        var found = await app.Services.GetRequiredService<SiteProvisioner>().FindUnlistedAsync();

        Assert.Contains(unlisted, found);
        Assert.DoesNotContain(app.FirstSiteId, found);
        Assert.Contains(unlisted, await schemas.GetSiteIdsAsync());
        Assert.Contains(unlisted, ImageStorage.SiteIdsWithFolders(storageSettings));

        await schemas.DropAsync(unlisted);
        ImageStorage.ForSite(storageSettings, unlisted).DeleteAll();
    }
}
