using ArtistShop.Web.Components;
using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.App;

// a button component's settings page, what it shows visitors, and its going when what it links to is deleted
public sealed class ButtonComponentTests(TestApp app)
{
    private sealed record ButtonPage(CustomPageId PageId, PageRowId RowId, CustomComponentId ButtonId, ButtonSettings Settings)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(ButtonId, PageId, RowId);
    }

    // a page at /buttons whose one row holds a new button, as the Add component dialog makes one
    private async Task<ButtonPage> AddButtonPageAsync(TestSite site)
    {
        var database = app.DatabaseOf(site);
        var pages = new CustomPageRepository(database);
        var pageId = await pages.AddAsync("Buttons", new CustomPageAddress("buttons"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await pages.GetLayoutAsync(pageId)).Rows).Slots);
        var settings = Assert.IsType<ButtonSettings>(NewCustomComponentSettings.For(CustomComponentStarter.Button, (await pages.GetHomeAsync()).Id));
        var buttonId = await new CustomComponentRepository(database).AddToSlotAsync(slot.Id, "Button 1", settings);

        return new ButtonPage(pageId, rowId, buttonId, settings);
    }

    private async Task<CustomPage> AddPageAsync(TestSite site, string address)
    {
        var pages = new CustomPageRepository(app.DatabaseOf(site));
        return Assert.IsType<CustomPage>(await pages.GetAsync(await pages.AddAsync($"Page {address}", new CustomPageAddress(address))));
    }

    private async Task<CustomComponentSettings?> SettingsOfAsync(TestSite site, CustomComponentId id) =>
        (await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(id))?.Component.Settings;

    private Task<string> PublicPageAsync(TestSite site) =>
        app.ClientFor(site.Host).GetStringAsync("/buttons", TestContext.Current.CancellationToken);

    private static Dictionary<string, string> ButtonForm(ButtonSettings button, Dictionary<string, string> link) =>
        new(link)
        {
            ["Input.Name"] = "Button 1",
            ["Input.Frame.Type"] = nameof(ComponentFrameType.Off),
            ["Input.ButtonText"] = button.Text,
            ["Input.Alignment"] = button.Alignment.ToString(),
            ["Input.ButtonStyle"] = button.Style.ToString(),
            ["Input.ButtonSize"] = button.Size.ToString(),
            ["Input.ButtonColor"] = button.Color.ToString(),
            ["Input.ButtonBorderWidth"] = button.BorderWidth?.ToString() ?? "",
            ["Input.ButtonCorners"] = button.Corners?.ToString() ?? "",
        };

    private Task LinkAsync(TestSite site, CustomComponentId id, ButtonSettings button) =>
        new CustomComponentRepository(app.DatabaseOf(site)).UpdateAsync(id, "Button 1", button, new ComponentFrame.Off());

    [Fact]
    public async Task TheSettingsPageSavesEveryChoice()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var about = await AddPageAsync(site, "about");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var chosen = new ButtonSettings("Read more", new LinkTarget.Page(about.Id), ComponentAlignment.Center, ButtonStyle.Outline,
            Size: 9, ButtonColor.Primary, FrameBorderWidth.Thick, FrameCorners.Large);

        await TestApp.PostFormAsync(
            client,
            buttonPage.SettingsUrl,
            "custom-component",
            ButtonForm(chosen, new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Page), ["Input.LinkTo.PageId"] = about.Id.Value.ToString() })
        );

        Assert.Equal(chosen, await SettingsOfAsync(site, buttonPage.ButtonId));
    }

    [Fact]
    public async Task AButtonMustLinkSomewhere()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            buttonPage.SettingsUrl,
            "custom-component",
            ButtonForm(buttonPage.Settings with { Text = "Shop" }, [])
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(buttonPage.Settings, await SettingsOfAsync(site, buttonPage.ButtonId));
    }

    [Fact]
    public async Task ASizeTheSliderCantMakeIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            buttonPage.SettingsUrl,
            "custom-component",
            ButtonForm(buttonPage.Settings with { Size = ButtonSettings.MaximumSize + 1 },
                new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Address), ["Input.LinkTo.Address"] = "https://example.com" })
        );

        Assert.Equal(buttonPage.Settings, await SettingsOfAsync(site, buttonPage.ButtonId));
    }

    // so it shows at once in the preview
    [Fact]
    public async Task ANewButtonLinksToTheHomePage()
    {
        var site = await app.MakeSiteAsync();
        await AddButtonPageAsync(site);
        var home = await new CustomPageRepository(app.DatabaseOf(site)).GetHomeAsync();

        Assert.Contains($"href=\"{PageUrls.CustomPage(home)}\" class=\"{ButtonClasses.Of(ButtonSettings.New(new LinkTarget.Page(home.Id)))}\"", await PublicPageAsync(site));
    }

    [Fact]
    public async Task AButtonHasNoFrame()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var settingsPage = await client.GetStringAsync(buttonPage.SettingsUrl, TestContext.Current.CancellationToken);
        await TestApp.PostFormAsync(
            client,
            buttonPage.SettingsUrl,
            "custom-component",
            new(ButtonForm(buttonPage.Settings, new() { ["Input.LinkTo.Type"] = nameof(LinkTargetType.Address), ["Input.LinkTo.Address"] = "https://example.com" }))
            {
                ["Input.Frame.Type"] = nameof(ComponentFrameType.Theme),
            }
        );

        Assert.DoesNotContain("name=\"Input.Frame.Type\"", settingsPage);
        Assert.IsType<ComponentFrame.Off>(Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(buttonPage.ButtonId)).Component.Frame);
        Assert.Equal(new LinkTarget.Address("https://example.com"), Assert.IsType<ButtonSettings>(await SettingsOfAsync(site, buttonPage.ButtonId)).LinkTarget);
    }

    [Fact]
    public async Task ALinkedButtonIsALinkDrawnInItsChoices()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var button = buttonPage.Settings with
        {
            LinkTarget = new LinkTarget.Address("https://example.com/shop"),
            Color = ButtonColor.Normal,
            Corners = FrameCorners.Medium,
        };
        await LinkAsync(site, buttonPage.ButtonId, button);

        var page = await PublicPageAsync(site);

        Assert.Contains($"<a href=\"https://example.com/shop\" class=\"{ButtonClasses.Of(button)}\">{button.Text}</a>", page);
        Assert.Contains($"style=\"{ButtonClasses.Style(button)}\"", page);
    }

    [Fact]
    public async Task DeletingWhatAButtonLinksToDeletesTheButton()
    {
        var site = await app.MakeSiteAsync();
        var buttonPage = await AddButtonPageAsync(site);
        var about = await AddPageAsync(site, "about");
        await LinkAsync(site, buttonPage.ButtonId, buttonPage.Settings with { LinkTarget = new LinkTarget.Page(about.Id) });
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var editor = await client.GetStringAsync(PageUrls.EditCustomPage(about.Id), TestContext.Current.CancellationToken);
        await new CustomPageRepository(app.DatabaseOf(site)).DeleteAsync(about.Id);

        Assert.Contains("data-test=\"deleted-buttons\"", editor);
        Assert.Null(await SettingsOfAsync(site, buttonPage.ButtonId));
        Assert.Empty(Assert.Single((await new CustomPageRepository(app.DatabaseOf(site)).GetLayoutAsync(buttonPage.PageId)).Rows).Slots[0].Stack);
    }
}
