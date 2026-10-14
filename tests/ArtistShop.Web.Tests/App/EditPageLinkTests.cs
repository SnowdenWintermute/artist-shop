using ArtistShop.Web.Components;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the footer's way from a public page to the admin page that edits it, for admins only
public sealed class EditPageLinkTests(TestApp app)
{
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    private static string EditLink(string href) => $"<a href=\"{href}\" class=\"text-link underline\">Edit page</a>";

    [Fact]
    public async Task ACollectionPageLinksAdminsToItsEditor()
    {
        var site = await app.MakeSiteAsync();
        var collectionId = await new CatalogTestData(DatabaseOf(site)).AddCollectionAsync();
        var collection = Assert.IsType<Collection>(await new CollectionRepository(DatabaseOf(site)).GetByIdAsync(collectionId));
        var path = PageUrls.Collection(collection.Slug);
        var admin = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        Assert.Contains(EditLink(PageUrls.EditCollection(collectionId)), await admin.GetStringAsync(path, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("Edit page", await app.ClientFor(site.Host).GetStringAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheHomePageLinksAdminsToItsPageEditor()
    {
        var site = await app.MakeSiteAsync();
        var home = await new CustomPageRepository(DatabaseOf(site)).GetHomeAsync();
        var admin = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        Assert.Contains(EditLink(PageUrls.EditCustomPage(home.Id)), await admin.GetStringAsync("/", TestContext.Current.CancellationToken));
    }
}
