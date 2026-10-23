using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.App;

// a heading's or an image's link: chosen on its settings page, drawn for visitors, and taken away when what it
// links to is deleted
public sealed class ComponentLinkTests(TestApp app)
{
    private sealed record LinkedPage(CustomPageId PageId, PageRowId RowId, RowSlotId SlotId, CustomComponentId HeadingId)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(HeadingId, PageId, RowId);
    }

    // a page at /linked whose one row holds a new heading component
    private async Task<LinkedPage> AddLinkedPageAsync(TestSite site)
    {
        var database = app.DatabaseOf(site);
        var pageId = await new CustomPageRepository(database).AddAsync("Linked", new CustomPageAddress("linked"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await new CustomPageRepository(database).GetLayoutAsync(pageId)).Rows).Slots);
        var headingId = await new CustomComponentRepository(database).AddToSlotAsync(slot.Id, "Heading 1", HeadingSettings.New);

        return new LinkedPage(pageId, rowId, slot.Id, headingId);
    }

    private async Task<CustomPage> AddPageAsync(TestSite site, string address)
    {
        var pages = new CustomPageRepository(app.DatabaseOf(site));
        return Assert.IsType<CustomPage>(await pages.GetAsync(await pages.AddAsync($"Page {address}", new CustomPageAddress(address))));
    }

    private Task<PostId> AddPostAsync(TestSite site, string title) =>
        new PostRepository(app.DatabaseOf(site)).AddAsync(
            new PostTitle(title),
            PostSlug.FromTitle(title),
            new RichText("""{"ops":[{"insert":"Hello\n"}]}"""),
            PostStatus.Published
        );

    private async Task<Work> AddPaintingAsync(TestSite site)
    {
        var identifiers = await new CatalogTestData(app.DatabaseOf(site)).AddPaintingAsync(
            $"Link test painting {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            [CatalogTestData.CreateTestImage()]
        );
        return Assert.IsType<Work>(await new WorkRepository(app.DatabaseOf(site)).GetByIdAsync(identifiers.Id));
    }

    private Task LinkAsync(TestSite site, CustomComponentId id, LinkTarget target) =>
        new CustomComponentRepository(app.DatabaseOf(site)).UpdateAsync(id, "Heading 1", HeadingSettings.New with { LinkTarget = target },
            new ComponentFrame.Off());

    private async Task<CustomComponentSettings> SettingsOfAsync(TestSite site, CustomComponentId id) =>
        Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(id)).Component.Settings;

    private async Task<LinkTarget?> HeadingLinkOfAsync(TestSite site, CustomComponentId id) =>
        Assert.IsType<HeadingSettings>(await SettingsOfAsync(site, id)).LinkTarget;

    private Task<string> PublicPageAsync(TestSite site) =>
        app.ClientFor(site.Host).GetStringAsync("/linked", TestContext.Current.CancellationToken);

    private static Dictionary<string, string> HeadingForm(Dictionary<string, string> link) =>
        new(link)
        {
            ["Input.Name"] = "Heading 1",
            ["Input.Frame.Type"] = nameof(ComponentFrameType.Off),
            ["Input.HeadingText"] = HeadingSettings.New.Text,
            ["Input.Size"] = HeadingSettings.New.Size.ToString(),
            ["Input.Font"] = HeadingSettings.New.Font.ToString(),
            ["Input.Bold"] = "true",
            ["Input.Alignment"] = HeadingSettings.New.Alignment.ToString(),
        };

    [Fact]
    public async Task AHeadingLinkedToAPageLinksThere()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var about = await AddPageAsync(site, "about");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            linked.SettingsUrl,
            "custom-component",
            HeadingForm(new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Page), ["Input.LinkTo.PageId"] = about.Id.Value.ToString() })
        );

        Assert.Equal(new LinkTarget.Page(about.Id), await HeadingLinkOfAsync(site, linked.HeadingId));
        Assert.Contains($"href=\"{PageUrls.CustomPage(about)}\"", await PublicPageAsync(site));
    }

    // as a card's title is
    [Fact]
    public async Task AHeadingLinkLooksLikeItsText()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var about = await AddPageAsync(site, "about");
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Page(about.Id));

        Assert.Contains($"<a href=\"{PageUrls.CustomPage(about)}\">{HeadingSettings.New.Text}</a>", await PublicPageAsync(site));
    }

    // choosing the type saves at once, before a page is picked
    [Fact]
    public async Task APageLinkWithNoPageChosenLinksToTheHomePage()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var home = await new CustomPageRepository(app.DatabaseOf(site)).GetHomeAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, linked.SettingsUrl, "custom-component",
            HeadingForm(new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Page) }));

        Assert.Equal(new LinkTarget.Page(home.Id), await HeadingLinkOfAsync(site, linked.HeadingId));
    }

    [Fact]
    public async Task ACollectionLinkWithNoCollectionChosenLinksToTheFirstOne()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        await catalog.AddCollectionAsync();
        await catalog.AddCollectionAsync();
        var first = (await new CollectionRepository(app.DatabaseOf(site)).GetAllAsync())[0];
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, linked.SettingsUrl, "custom-component",
            HeadingForm(new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Collection) }));

        Assert.Equal(new LinkTarget.Collection(first.Id), await HeadingLinkOfAsync(site, linked.HeadingId));
    }

    // as a new button does
    [Fact]
    public async Task AnImageSetToLinkStartsAtTheHomePage()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var home = await new CustomPageRepository(app.DatabaseOf(site)).GetHomeAsync();
        var imageId = await new CustomComponentRepository(app.DatabaseOf(site)).AddToSlotAsync(linked.SlotId, "Image 1", ImageSettings.New);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.EditCustomComponent(imageId, linked.PageId, linked.RowId),
            "custom-component",
            new()
            {
                ["Input.Name"] = "Image 1",
                ["Input.Frame.Type"] = nameof(ComponentFrameType.Off),
                ["Input.ImageHeight"] = nameof(ImageHeight.Natural),
                ["Input.Fit"] = nameof(ImageFit.Cover),
                ["Input.Link"] = nameof(ImageLink.Target),
            }
        );

        Assert.Equal(new LinkTarget.Page(home.Id), Assert.IsType<ImageSettings>(await SettingsOfAsync(site, imageId)).LinkTarget);
    }

    [Fact]
    public async Task AnAddressThatIsntWholeIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            linked.SettingsUrl,
            "custom-component",
            HeadingForm(new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Address), ["Input.LinkTo.Address"] = "example.com" })
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await HeadingLinkOfAsync(site, linked.HeadingId));
    }

    // the address is read as the page is drawn, so the link isn't left at the old one
    [Fact]
    public async Task ALinkFollowsItsPageToANewAddress()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var about = await AddPageAsync(site, "about");
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Page(about.Id));

        await new CustomPageRepository(app.DatabaseOf(site)).UpdateAsync(about.Id, about.Name, new CustomPageAddress("me"));

        Assert.Contains("href=\"/me\"", await PublicPageAsync(site));
    }

    [Fact]
    public async Task AnImageLinkedToAPostLinksToIt()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var work = await AddPaintingAsync(site);
        var postId = await AddPostAsync(site, "Spring");
        var components = new CustomComponentRepository(app.DatabaseOf(site));
        var imageId = await components.AddToSlotAsync(linked.SlotId, "Image 1", ImageSettings.New);

        await components.UpdateAsync(
            imageId,
            "Image 1",
            ImageSettings.New with { Source = new ImageSource.Work(work.Id, StorageKey: null), Link = ImageLink.Target, LinkTarget = new LinkTarget.Post(postId) },
            new ComponentFrame.Off()
        );

        Assert.Contains($"href=\"{PageUrls.Post(PostSlug.FromTitle("Spring"))}\"", await PublicPageAsync(site));
    }

    [Fact]
    public async Task DeletingALinkedPageTakesTheLinkAway()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var about = await AddPageAsync(site, "about");
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Page(about.Id));

        await new CustomPageRepository(app.DatabaseOf(site)).DeleteAsync(about.Id);

        Assert.Null(await HeadingLinkOfAsync(site, linked.HeadingId));
    }

    [Fact]
    public async Task DeletingALinkedCollectionOrPostTakesTheLinkAway()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var collectionId = await new CatalogTestData(app.DatabaseOf(site)).AddCollectionAsync();
        var postId = await AddPostAsync(site, "Summer");
        var components = new CustomComponentRepository(app.DatabaseOf(site));
        var postHeadingId = await components.AddToSlotAsync(linked.SlotId, "Heading 2", HeadingSettings.New);
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Collection(collectionId));
        await components.UpdateAsync(postHeadingId, "Heading 2", HeadingSettings.New with { LinkTarget = new LinkTarget.Post(postId) },
            new ComponentFrame.Off());

        await new CollectionRepository(app.DatabaseOf(site)).DeleteAsync(collectionId);
        await new PostRepository(app.DatabaseOf(site)).DeleteAsync(postId);

        Assert.Null(await HeadingLinkOfAsync(site, linked.HeadingId));
        Assert.Null(await HeadingLinkOfAsync(site, postHeadingId));
    }

    [Fact]
    public async Task DeletingALinkedWorkLeavesAnImageWithNoLink()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var shown = await AddPaintingAsync(site);
        var linkedWork = await AddPaintingAsync(site);
        var components = new CustomComponentRepository(app.DatabaseOf(site));
        var imageId = await components.AddToSlotAsync(linked.SlotId, "Image 1", ImageSettings.New);
        var settings = ImageSettings.New with { Source = new ImageSource.Work(shown.Id, StorageKey: null) };
        await components.UpdateAsync(imageId, "Image 1", settings with { Link = ImageLink.Target, LinkTarget = new LinkTarget.Work(linkedWork.Id) },
            new ComponentFrame.Off());

        await new WorkRepository(app.DatabaseOf(site)).DeleteAsync(linkedWork.Id);

        Assert.Equal(settings with { Link = ImageLink.None }, await SettingsOfAsync(site, imageId));
    }

    [Fact]
    public async Task CombiningWorksMovesTheLinkToTheKeptWork()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var kept = await AddPaintingAsync(site);
        var other = await AddPaintingAsync(site);
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Work(other.Id));

        await new WorkRepository(app.DatabaseOf(site)).CombineAsync(kept.Id, [other.Id]);

        Assert.Equal(new LinkTarget.Work(kept.Id), await HeadingLinkOfAsync(site, linked.HeadingId));
        Assert.Contains($"href=\"{WorkPageQuery.Url(kept.Slug.Value, collectionSlug: null, imageNumber: null)}\"", await PublicPageAsync(site));
    }

    [Fact]
    public async Task ThePagesDeleteDialogListsTheComponentsLinkingThere()
    {
        var site = await app.MakeSiteAsync();
        var linked = await AddLinkedPageAsync(site);
        var about = await AddPageAsync(site, "about");
        await LinkAsync(site, linked.HeadingId, new LinkTarget.Page(about.Id));
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var editor = await client.GetStringAsync(PageUrls.EditCustomPage(about.Id), TestContext.Current.CancellationToken);

        Assert.Contains("data-test=\"unlinked-components\"", editor);
        Assert.Contains("Heading 1", editor);
    }
}
