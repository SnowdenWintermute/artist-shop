using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The Colors page, where an artist previews and saves the colours of their website's public pages
[Collection(TestAppCollection.Name)]
public sealed class ColorsPageTests(TestApp app)
{
    private ColorRepository ColorsOf(TestSite site) => new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    // every role as the page shows it with nothing chosen, then whatever the test changes, as a
    // browser posts the form
    private static Dictionary<string, string> Fields(Action<Dictionary<string, string>, Func<ColorRole, string>> change)
    {
        var fields = new Dictionary<string, string>();

        for (var index = 0; index < ColorRoles.All.Count; index++)
        {
            var definition = ColorRoles.All[index];
            var hex = (definition.Platform with { Alpha = 255 }).Hex;
            fields[$"Input.Roles[{index}].Role"] = definition.Role.ToString();
            fields[$"Input.Roles[{index}].IsDerived"] = "true";
            fields[$"Input.Roles[{index}].Color"] = hex;
            fields[$"Input.Roles[{index}].ShownColor"] = hex;
            fields[$"Input.Roles[{index}].OpacityPercent"] = definition.Platform.OpacityPercent.ToString();
            fields[$"Input.Roles[{index}].ShownOpacityPercent"] = definition.Platform.OpacityPercent.ToString();
        }

        change(fields, role => $"Input.Roles[{ColorRoles.All.ToList().FindIndex(definition => definition.Role == role)}]");
        return fields;
    }

    private async Task<string> PostAsync(TestSite site, Dictionary<string, string> fields)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var response = await TestApp.PostFormAsync(client, PageUrls.Colors, "colors", fields);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryPageHasThePlatformsColors()
    {
        var site = await app.MakeSiteAsync();

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains(":root { --theme-page: #f5f5f5;", page);
    }

    // on :root only while a public page's marker is there, so the nav bar and the page's edges follow
    private const string SiteColorsSelector = ":root:has([data-site-colors])";

    [Fact]
    public async Task PublicPagesHaveTheWebsitesColors()
    {
        var site = await app.MakeSiteAsync();
        await ColorsOf(site).UpdateAsync(new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Page] = RgbColor.Parse("#202020") }));
        var visitor = app.ClientFor(site.Host);

        foreach (var path in new[] { "/", PageUrls.Blog, "/not-found" })
        {
            var page = await visitor.GetStringAsync(path, TestContext.Current.CancellationToken);

            Assert.Contains($"{SiteColorsSelector} {{ --theme-page: #202020;", page);
            Assert.Contains("data-site-colors", page);
        }
    }

    [Fact]
    public async Task AdminAndPlatformPagesKeepThePlatformsColors()
    {
        var site = await app.MakeSiteAsync();
        await ColorsOf(site).UpdateAsync(new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Page] = RgbColor.Parse("#202020") }));
        var owner = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var admin = await owner.GetStringAsync(PageUrls.AdminDashboard, TestContext.Current.CancellationToken);
        var platformHome = await app.ClientFor(TestApp.PlatformHost).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(SiteColorsSelector, admin);
        Assert.DoesNotContain("#202020", admin);
        Assert.DoesNotContain(SiteColorsSelector, platformHome);
    }

    // picking is all the artist did
    [Fact]
    public async Task PickingAColorPreviewsItWithoutSaving()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostAsync(site, Fields((fields, role) => fields[$"{role(ColorRole.Page)}.Color"] = "#202020"));

        Assert.Contains("[data-color-preview] { --theme-page: #202020;", page);
        Assert.Contains("Not saved yet.", page);
        Assert.Empty((await ColorsOf(site).GetAsync()).Chosen);
    }

    [Fact]
    public async Task SaveKeepsTheChosenColorsAndLeavesTheRestDerived()
    {
        var site = await app.MakeSiteAsync();

        await PostAsync(
            site,
            Fields(
                (fields, role) =>
                {
                    fields[$"{role(ColorRole.Accent)}.Color"] = "#aa3300";
                    fields[$"{role(ColorRole.Backdrop)}.OpacityPercent"] = "70";
                    fields["Input.Save"] = "true";
                }
            )
        );

        Assert.Equal(
            new Dictionary<ColorRole, RgbColor>
            {
                [ColorRole.Accent] = RgbColor.Parse("#aa3300"),
                [ColorRole.Backdrop] = RgbColor.Black.WithOpacityPercent(70),
            },
            (await ColorsOf(site).GetAsync()).Chosen
        );
    }

    // a reset previews like a pick, and Save keeps it
    [Fact]
    public async Task ResetPutsARoleBackToItsDefault()
    {
        var site = await app.MakeSiteAsync();
        await ColorsOf(site).UpdateAsync(
            new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Bar] = RgbColor.Parse("#aa0000"), [ColorRole.Link] = RgbColor.Parse("#00aa00") })
        );
        void BothChosen(Dictionary<string, string> fields, Func<ColorRole, string> role)
        {
            foreach (var (chosen, hex) in new[] { (ColorRole.Bar, "#aa0000"), (ColorRole.Link, "#00aa00") })
            {
                fields[$"{role(chosen)}.IsDerived"] = "false";
                fields[$"{role(chosen)}.Color"] = hex;
                fields[$"{role(chosen)}.ShownColor"] = hex;
            }

            fields["Input.Reset"] = nameof(ColorRole.Bar);
        }

        var page = await PostAsync(site, Fields(BothChosen));
        Assert.Contains("--theme-bar: #e9e7e2;", page);
        Assert.Contains("--theme-link: #00aa00;", page);
        Assert.Equal(2, (await ColorsOf(site).GetAsync()).Chosen.Count);

        await PostAsync(
            site,
            Fields(
                (fields, role) =>
                {
                    BothChosen(fields, role);
                    fields["Input.Save"] = "true";
                }
            )
        );
        Assert.Equal([ColorRole.Link], (await ColorsOf(site).GetAsync()).Chosen.Keys);
    }

    [Fact]
    public async Task ResetAllPreviewsEveryDefault()
    {
        var site = await app.MakeSiteAsync();
        await ColorsOf(site).UpdateAsync(new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Page] = RgbColor.Parse("#202020") }));

        var page = await PostAsync(
            site,
            Fields(
                (fields, role) =>
                {
                    fields[$"{role(ColorRole.Page)}.IsDerived"] = "false";
                    fields[$"{role(ColorRole.Page)}.Color"] = "#202020";
                    fields[$"{role(ColorRole.Page)}.ShownColor"] = "#202020";
                    fields["Input.ResetAll"] = "true";
                }
            )
        );

        Assert.Contains("[data-color-preview] { --theme-page: #f5f5f5;", page);
        Assert.DoesNotContain("Reset all to defaults", page);
        Assert.Single((await ColorsOf(site).GetAsync()).Chosen);
    }

    [Fact]
    public async Task ColorsHardToReadAreWarnedOf()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostAsync(site, Fields((fields, role) => fields[$"{role(ColorRole.InkFaded)}.Color"] = "#eeeeee"));

        Assert.Contains("Hard to read on background", page);
    }

    [Fact]
    public async Task APostMissingARoleIsRefused()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostAsync(
            site,
            Fields(
                (fields, _) =>
                {
                    foreach (var key in fields.Keys.Where(key => key.StartsWith("Input.Roles[14]", StringComparison.Ordinal)).ToList())
                    {
                        fields.Remove(key);
                    }

                    fields["Input.Save"] = "true";
                }
            )
        );

        Assert.Contains("don&#x27;t match this page", page);
        Assert.Empty((await ColorsOf(site).GetAsync()).Chosen);
    }
}
