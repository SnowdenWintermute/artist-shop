using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the components list, and a component's settings page and preview opened from it
public sealed class CustomComponentListTests(TestApp app)
{
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    // a heading added to the home page's one slot, which no page needs
    private async Task<(RowSlotId SlotId, CustomComponentId HeadingId)> AddHeadingToHomeAsync(TestSite site)
    {
        var pages = new CustomPageRepository(DatabaseOf(site));
        var home = await pages.GetHomeAsync();
        var slot = Assert.Single(Assert.Single((await pages.GetLayoutAsync(home.Id)).Rows).Slots);
        var headingId = await new CustomComponentRepository(DatabaseOf(site)).AddToSlotAsync(slot.Id, "Heading 1", HeadingSettings.New);

        return (slot.Id, headingId);
    }

    private static string ListItemOf(string list, CustomComponentId id) =>
        Regex.Match(list, $"<li[^>]* data-test=\"component-{id.Value}\".*?</li>", RegexOptions.Singleline).Value;

    private static string DeleteButtonOf(CustomComponentId id) => $"data-opens-dialog=\"delete-component-{id.Value}\"";

    [Fact]
    public async Task TheListMarksAComponentTakenOffItsPage()
    {
        var site = await app.MakeSiteAsync();
        var pages = new CustomPageRepository(DatabaseOf(site));
        var homeGrid = Assert.Single(Assert.Single(Assert.Single((await pages.GetLayoutAsync((await pages.GetHomeAsync()).Id)).Rows).Slots).Stack);
        var (slotId, headingId) = await AddHeadingToHomeAsync(site);
        await new RowSlotRepository(DatabaseOf(site)).RemovePlacementAsync(slotId, headingId);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var list = await client.GetStringAsync(PageUrls.CustomComponentList, TestContext.Current.CancellationToken);

        Assert.Contains("data-test=\"not-on-any-page\"", ListItemOf(list, headingId));
        Assert.DoesNotContain("data-test=\"not-on-any-page\"", ListItemOf(list, homeGrid.Id));
    }

    // the last post list on the blog page and grid of all collections on the collections page stay
    [Fact]
    public async Task OnlyComponentsThatCanGoHaveADeleteButton()
    {
        var site = await app.MakeSiteAsync();
        var pages = new CustomPageRepository(DatabaseOf(site));
        var postList = Assert.Single(Assert.Single(Assert.Single((await pages.GetLayoutAsync((await pages.GetBlogAsync()).Id)).Rows).Slots).Stack);
        var collectionsGrid = Assert.Single(Assert.Single(Assert.Single((await pages.GetLayoutAsync((await pages.GetCollectionsAsync()).Id)).Rows).Slots).Stack);
        var (_, headingId) = await AddHeadingToHomeAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var list = await client.GetStringAsync(PageUrls.CustomComponentList, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(DeleteButtonOf(postList.Id), list);
        Assert.DoesNotContain(DeleteButtonOf(collectionsGrid.Id), list);
        Assert.Contains(DeleteButtonOf(headingId), list);
    }

    [Fact]
    public async Task AComponentIsDeletedFromTheList()
    {
        var site = await app.MakeSiteAsync();
        var (_, headingId) = await AddHeadingToHomeAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await client.GetStringAsync(PageUrls.CustomComponentList, TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(client, PageUrls.CustomComponentList, $"delete-component-{headingId.Value}", []);

        Assert.Equal(PageUrls.CustomComponentList, response.Headers.Location?.AbsolutePath);
        Assert.Null(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(headingId));
    }

    // as when the list was read before the blog page came to need it: the post still finds its form
    [Fact]
    public async Task ARefusedDeleteSaysWhy()
    {
        var site = await app.MakeSiteAsync();
        var pages = new CustomPageRepository(DatabaseOf(site));
        var postList = Assert.Single(Assert.Single(Assert.Single((await pages.GetLayoutAsync((await pages.GetBlogAsync()).Id)).Rows).Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await client.GetStringAsync(PageUrls.CustomComponentList, TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(client, PageUrls.CustomComponentList, $"delete-component-{postList.Id.Value}", []);
        var list = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-test=\"delete-refused\"", ListItemOf(list, postList.Id));
        Assert.NotNull(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(postList.Id));
    }

    [Fact]
    public async Task AComponentOpenedFromTheListLinksUpToItAndPreviewsItAlone()
    {
        var site = await app.MakeSiteAsync();
        var (_, headingId) = await AddHeadingToHomeAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settings = await client.GetStringAsync(PageUrls.EditCustomComponent(headingId, origin: null), TestContext.Current.CancellationToken);

        Assert.Matches($"<a href=\"{Regex.Escape(PageUrls.CustomComponentList)}\"[^>]* data-test=\"up-link\"", settings);
        Assert.Matches($"<iframe[^>]* src=\"{Regex.Escape(PageUrls.CustomComponentPreview(headingId))}\"[^>]* data-test=\"page-preview\"", settings);
    }

    // a post list too, which on a page is headed with the page's name
    [Fact]
    public async Task TheComponentPreviewDrawsTheComponentAlone()
    {
        var site = await app.MakeSiteAsync();
        var pages = new CustomPageRepository(DatabaseOf(site));
        var postList = Assert.Single(Assert.Single(Assert.Single((await pages.GetLayoutAsync((await pages.GetBlogAsync()).Id)).Rows).Slots).Stack);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var preview = await client.GetStringAsync(PageUrls.CustomComponentPreview(postList.Id), TestContext.Current.CancellationToken);

        Assert.Contains($"data-component-id=\"{postList.Id.Value}\"", preview);
        Assert.Single(Regex.Matches(preview, "data-component-id="));
    }
}
