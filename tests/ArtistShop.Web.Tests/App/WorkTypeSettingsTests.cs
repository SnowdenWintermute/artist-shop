using System.Net;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.App;

// the site-wide unit for works' sizes: what the public work page shows and what the admin forms take
public sealed class WorkTypeSettingsTests(TestApp app)
{
    [Fact]
    public async Task ANewSiteShowsCentimetres()
    {
        var site = await app.MakeSiteAsync();

        var settings = await new WorkTypeSettingsRepository(app.DatabaseOf(site)).GetAsync();

        Assert.Equal(WorkTypeSettings.Default, settings);
        Assert.Equal(DimensionDisplay.Centimeters, settings.DimensionDisplay);
    }

    [Fact]
    public async Task TheSettingsPageSavesTheChoice()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.WorkTypeSettings,
            "work-type-settings",
            new() { ["Input.DimensionDisplay"] = nameof(DimensionDisplay.InchesThenCentimeters) }
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            new WorkTypeSettings(DimensionDisplay.InchesThenCentimeters),
            await new WorkTypeSettingsRepository(app.DatabaseOf(site)).GetAsync()
        );
    }

    // a post a script made
    [Fact]
    public async Task AnUnknownChoiceIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.WorkTypeSettings,
            "work-type-settings",
            new() { ["Input.DimensionDisplay"] = "7" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkTypeSettings.Default, await new WorkTypeSettingsRepository(app.DatabaseOf(site)).GetAsync());
    }

    [Fact]
    public async Task TheWorkTypeTabsLinkToTheSettings()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.NewWorkType, TestContext.Current.CancellationToken);

        Assert.Contains($"href=\"{PageUrls.WorkTypeSettings}\"", page);
    }

    [Fact]
    public async Task TheWorkPageShowsTheSizeInTheChosenUnits()
    {
        var site = await app.MakeSiteAsync();
        var database = app.DatabaseOf(site);
        var catalog = new CatalogTestData(database);
        var dimensions = new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null));
        var work = await catalog.AddWorkWithDimensionsAsync(await catalog.GetPaintingTypeIdAsync(), dimensions);
        await new WorkTypeSettingsRepository(database).UpdateAsync(new WorkTypeSettings(DimensionDisplay.InchesThenCentimeters));

        var page = await app.ClientFor(site.Host).GetStringAsync($"/works/{work.Slug.Value}", TestContext.Current.CancellationToken);

        // the page writes × as a character reference
        Assert.Contains(dimensions.Text(DimensionDisplay.InchesThenCentimeters), WebUtility.HtmlDecode(page));
    }

    // a size saved in centimetres and shown in inches stays put unless the artist changes it
    [Fact]
    public async Task TheEditPageTakesInchesWithoutMovingAnUnchangedSize()
    {
        var site = await app.MakeSiteAsync();
        var database = app.DatabaseOf(site);
        var catalog = new CatalogTestData(database);
        var identifiers = await catalog.AddWorkWithDimensionsAsync(
            await catalog.GetPaintingTypeIdAsync(),
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null))
        );
        await new WorkTypeSettingsRepository(database).UpdateAsync(new WorkTypeSettings(DimensionDisplay.Inches));
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var works = new WorkRepository(database);
        var name = Assert.IsType<Work>(await works.GetByIdAsync(identifiers.Id)).Name.Value;

        var page = await client.GetStringAsync(PageUrls.EditWork(identifiers.Id), TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.EditWork(identifiers.Id),
            "edit-work",
            new()
            {
                ["Input.Name"] = name,
                ["Input.LengthUnit"] = nameof(LengthUnit.Inches),
                ["Input.Height"] = "11.811",
                ["Input.Width"] = "16",
            }
        );

        Assert.Contains("Height (in)", page);
        Assert.Contains("value=\"11.8110\"", page);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            new DimensionsCentimeters(new Dimensions(30m, 40.64m, depth: null)),
            Assert.IsType<Work>(await works.GetByIdAsync(identifiers.Id)).Dimensions
        );
    }
}
