using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Layout;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.App;

// the frame every component has: chosen on its settings page, drawn round it for visitors
public sealed class ComponentFrameTests(TestApp app)
{
    private sealed record HeadingPage(CustomPageId PageId, PageRowId RowId, CustomComponentId HeadingId)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(HeadingId, PageId, RowId);
    }

    // a page at /framed whose one row holds a new heading component
    private async Task<HeadingPage> AddHeadingPageAsync(TestSite site)
    {
        var database = app.DatabaseOf(site);
        var pageId = await new CustomPageRepository(database).AddAsync("Framed", new CustomPageAddress("framed"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await new CustomPageRepository(database).GetLayoutAsync(pageId)).Rows).Slots);
        var headingId = await new CustomComponentRepository(database).AddToSlotAsync(slot.Id, "Heading 1", HeadingSettings.New);

        return new HeadingPage(pageId, rowId, headingId);
    }

    private static Dictionary<string, string> HeadingForm(Dictionary<string, string> frame) =>
        new(frame)
        {
            ["Input.Name"] = "Heading 1",
            ["Input.HeadingText"] = HeadingSettings.New.Text,
            ["Input.Size"] = HeadingSettings.New.Size.ToString(),
            ["Input.Font"] = HeadingSettings.New.Font.ToString(),
            ["Input.Bold"] = "true",
            ["Input.Alignment"] = HeadingSettings.New.Alignment.ToString(),
        };

    private async Task<ComponentFrame> FrameOfAsync(TestSite site, CustomComponentId id) =>
        Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(id)).Component.Frame;

    private async Task FrameAsync(TestSite site, CustomComponentId id, ComponentFrame frame) =>
        await new CustomComponentRepository(app.DatabaseOf(site)).UpdateAsync(id, "Heading 1", HeadingSettings.New, frame);

    private Task<string> PublicPageAsync(TestSite site) =>
        app.ClientFor(site.Host).GetStringAsync("/framed", TestContext.Current.CancellationToken);

    [Fact]
    public async Task ACustomFrameKeepsOnlyThePartsChosen()
    {
        var site = await app.MakeSiteAsync();
        var headingPage = await AddHeadingPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            headingPage.SettingsUrl,
            "custom-component",
            HeadingForm(
                new()
                {
                    ["Input.Frame.Type"] = nameof(ComponentFrameType.Custom),
                    ["Input.Frame.HasOwnBackground"] = "true",
                    ["Input.Frame.Background"] = "#112233",
                    ["Input.Frame.BackgroundOpacityPercent"] = "40",
                    ["Input.Frame.HasOwnBorder"] = "false",
                    ["Input.Frame.Border"] = "#445566",
                    ["Input.Frame.BorderWidth"] = "",
                    ["Input.Frame.Corners"] = nameof(FrameCorners.Large),
                    ["Input.Frame.Padding"] = "",
                }
            )
        );

        Assert.Equal(
            new ComponentFrame.Custom(RgbColor.Parse("#112233").WithOpacityPercent(40), null, null, FrameCorners.Large, null),
            await FrameOfAsync(site, headingPage.HeadingId)
        );
    }

    // only a hand-written post sends a colour the picker can't
    [Fact]
    public async Task AFrameThePageCouldntHaveSentIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var headingPage = await AddHeadingPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            headingPage.SettingsUrl,
            "custom-component",
            HeadingForm(
                new()
                {
                    ["Input.Frame.Type"] = nameof(ComponentFrameType.Custom),
                    ["Input.Frame.HasOwnBackground"] = "true",
                    ["Input.Frame.Background"] = "red",
                }
            )
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.IsType<ComponentFrame.Off>(await FrameOfAsync(site, headingPage.HeadingId));
    }

    [Fact]
    public async Task AnUnframedComponentHasNoFrame()
    {
        var site = await app.MakeSiteAsync();
        await AddHeadingPageAsync(site);

        Assert.DoesNotContain("data-test=\"component-frame\"", await PublicPageAsync(site));
    }

    [Fact]
    public async Task TheThemesFrameIsDrawnRoundTheComponent()
    {
        var site = await app.MakeSiteAsync();
        var headingPage = await AddHeadingPageAsync(site);
        await FrameAsync(site, headingPage.HeadingId, new ComponentFrame.Theme());

        var page = await PublicPageAsync(site);

        Assert.Contains("data-test=\"component-frame\"", page);
    }

    [Fact]
    public async Task ACustomFrameSetsItsPartsOverTheThemes()
    {
        var site = await app.MakeSiteAsync();
        var headingPage = await AddHeadingPageAsync(site);
        var frame = new ComponentFrame.Custom(RgbColor.Parse("#112233").WithOpacityPercent(40), RgbColor.Parse("#445566"), FrameBorderWidth.Thick, null, FramePadding.None);
        await FrameAsync(site, headingPage.HeadingId, frame);

        var page = await PublicPageAsync(site);

        Assert.Contains($"style=\"{FrameCss.Overrides(frame)}\"", page);
    }
}
