using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The Theme page, where an artist previews, saves and chooses the look of their website's public pages
[Collection(TestAppCollection.Name)]
public sealed class ThemePageTests(TestApp app)
{
    private ThemeRepository ThemesOf(TestSite site) => new(app.Services.GetRequiredService<SiteDatabases>().For(site.Id));

    private static readonly ThemeColors Paper = ThemePresets.Paper.Theme.Colors;

    private static Theme Charcoal => new(new ThemeColors(new Dictionary<ColorRole, RgbColor>
    {
        [ColorRole.Page] = RgbColor.Parse("#202020"),
        [ColorRole.Ink] = RgbColor.White,
        [ColorRole.Accent] = RgbColor.Parse("#99c1f1"),
        [ColorRole.Bar] = RgbColor.Parse("#aa0000"),
    }), new ThemeFonts(new FontChoice(Font.Cinzel, 110), new FontChoice(Font.Bitter, 90)));

    // every font and role as the page shows the open theme, then whatever the test changes, as a browser
    // posts the form. Fonts at Input.Fonts[0] (heading) and [1] (text)
    private static Dictionary<string, string> Fields(Theme open, string name, Action<Dictionary<string, string>, Func<ColorRole, string>> change)
    {
        // the page's name and the Save as new theme dialog's, which a test posts whichever it needs
        var fields = new Dictionary<string, string> { ["Input.Name"] = name, ["Input.NewName"] = name };

        for (var index = 0; index < FontRoles.All.Count; index++)
        {
            var role = FontRoles.All[index].Role;
            fields[$"Input.Fonts[{index}].Role"] = role.ToString();
            fields[$"Input.Fonts[{index}].Font"] = open.Fonts.For(role).Font.ToString();
            fields[$"Input.Fonts[{index}].SizePercent"] = open.Fonts.For(role).SizePercent.ToString();
        }

        for (var index = 0; index < ColorRoles.All.Count; index++)
        {
            var role = ColorRoles.All[index].Role;
            var resolved = open.Colors.Resolve(role);
            var hex = (resolved with { Alpha = 255 }).Hex;
            fields[$"Input.Roles[{index}].Role"] = role.ToString();
            fields[$"Input.Roles[{index}].IsDerived"] = open.Colors.ChosenFor(role) is null ? "true" : "false";
            fields[$"Input.Roles[{index}].Color"] = hex;
            fields[$"Input.Roles[{index}].ShownColor"] = hex;
            fields[$"Input.Roles[{index}].OpacityPercent"] = resolved.OpacityPercent.ToString();
            fields[$"Input.Roles[{index}].ShownOpacityPercent"] = resolved.OpacityPercent.ToString();
        }

        change(fields, role => $"Input.Roles[{ColorRoles.All.ToList().FindIndex(definition => definition.Role == role)}]");
        return fields;
    }

    private static Dictionary<string, string> PaperFields(Action<Dictionary<string, string>, Func<ColorRole, string>> change) =>
        Fields(ThemePresets.Paper.Theme, "Paper copy", change);

    private async Task<string> PostAsync(TestSite site, string path, string formName, Dictionary<string, string> fields)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var response = await TestApp.PostFormAsync(client, path, formName, fields);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private Task<string> PostThemeAsync(TestSite site, Dictionary<string, string> fields, string path = PageUrls.Theme) =>
        PostAsync(site, path, "theme", fields);

    private async Task<string> GetAsync(TestSite site, string path)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        return await client.GetStringAsync(path, TestContext.Current.CancellationToken);
    }

    // the value of the input with that name, whatever order its attributes come in; null with no such input
    private static string? InputValue(string page, string name) =>
        Regex.Matches(page, "<input [^>]*>")
            .Select(input => input.Value)
            .Where(input => input.Contains($"name=\"{name}\"", StringComparison.Ordinal))
            .Select(input => Regex.Match(input, "value=\"(?<value>[^\"]*)\"").Groups["value"].Value)
            .FirstOrDefault();

    // on :root only while a public page's marker is there, so the nav bar and the page's edges follow
    private const string SiteThemeSelector = ":root:has([data-site-theme])";

    [Fact]
    public async Task EveryPageHasThePlatformsColors()
    {
        var site = await app.MakeSiteAsync();

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains(":root { --theme-page: #f5f5f5;", page);
        Assert.Contains($"{SiteThemeSelector} {{ --theme-page: #f5f5f5;", page);
    }

    [Fact]
    public async Task PublicPagesHaveTheThemeInUse()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);
        await ThemesOf(site).UseAsync(new ThemeKey.Saved(id));
        var visitor = app.ClientFor(site.Host);

        foreach (var path in new[] { "/", PageUrls.Blog, "/not-found" })
        {
            var page = await visitor.GetStringAsync(path, TestContext.Current.CancellationToken);

            Assert.Contains($"{SiteThemeSelector} {{ --theme-page: #202020;", page);
        }
    }

    [Fact]
    public async Task EveryPageHasThePlatformsFonts()
    {
        var site = await app.MakeSiteAsync();

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        var heading = ThemePresets.Paper.Theme.Fonts.Heading;
        var font = Fonts.For(heading.Font);
        var adjust = FontRoles.For(FontRole.Heading).SizeAdjust(heading.Font, heading.SizePercent);
        Assert.Contains($":root {{ --theme-heading-font: {font.CssFamily}; --theme-heading-font-size-adjust: {adjust};", page);
        Assert.Contains($"font-family: {font.CssFamily}; src: url(\"{font.Url(font.Faces[0])}\")", page);
    }

    [Fact]
    public async Task PublicPagesHaveTheFontsOfTheThemeInUse()
    {
        var site = await app.MakeSiteAsync();
        await ThemesOf(site).UseAsync(new ThemeKey.Saved(await ThemesOf(site).AddAsync("Charcoal", Charcoal)));

        var page = await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains($"{SiteThemeSelector} {{ --theme-heading-font: \"Cinzel\";", page);
        Assert.Contains("--theme-text-font: \"Bitter\";", page);
        Assert.Contains("font-family: \"Cinzel\"; src: url(\"/fonts/cinzel/Cinzel-Regular.woff2\")", page);
    }

    [Fact]
    public async Task PickingAFontPreviewsIt()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(site, PaperFields((fields, _) => fields["Input.Fonts[0].Font"] = nameof(Font.Pacifico)));

        Assert.Contains("[data-theme-preview] { --theme-heading-font: \"Pacifico\";", page);
        Assert.Contains("Heading font: Pacifico", page);
    }

    [Fact]
    public async Task SaveKeepsTheFontsAndTheirSizes()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        await PostThemeAsync(
            site,
            Fields(Charcoal, "Charcoal", (fields, _) =>
            {
                fields["Input.Fonts[1].Font"] = nameof(Font.OpenSans);
                fields["Input.Fonts[1].SizePercent"] = "120";
                fields["Input.Save"] = "true";
            }),
            PageUrls.ThemeEditing(new ThemeKey.Saved(id))
        );

        var saved = Assert.Single(await ThemesOf(site).GetAllAsync());
        Assert.Equal(new FontChoice(Font.OpenSans, 120), saved.Theme.Fonts.Text);
        Assert.Equal(Charcoal.Fonts.Heading, saved.Theme.Fonts.Heading);
    }

    // only a hand-written post can, since the page lists only the fonts each role allows
    [Fact]
    public async Task AHeadingOnlyFontIsRefusedAsTheTextFont()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var page = await PostThemeAsync(
            site,
            Fields(Charcoal, "Charcoal", (fields, _) =>
            {
                fields["Input.Fonts[1].Font"] = nameof(Font.Pacifico);
                fields["Input.Save"] = "true";
            }),
            PageUrls.ThemeEditing(new ThemeKey.Saved(id))
        );

        Assert.Contains("A font sent isn&#x27;t one this page could have picked", page);
        Assert.Equal(Charcoal.Fonts, Assert.Single(await ThemesOf(site).GetAllAsync()).Theme.Fonts);
    }

    [Fact]
    public async Task AFontsResetPutsBackTheFontAsSaved()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var page = await PostThemeAsync(
            site,
            Fields(Charcoal, "Charcoal", (fields, _) =>
            {
                fields["Input.Fonts[0].Font"] = nameof(Font.Pacifico);
                fields["Input.Fonts[1].Font"] = nameof(Font.OpenSans);
                fields["Input.ResetFont"] = nameof(FontRole.Heading);
            }),
            PageUrls.ThemeEditing(new ThemeKey.Saved(id))
        );

        Assert.Contains("[data-theme-preview] { --theme-heading-font: \"Cinzel\";", page);
        Assert.Contains("--theme-text-font: \"Open Sans\";", page);
    }

    [Fact]
    public async Task AdminAndPlatformPagesKeepThePlatformsColors()
    {
        var site = await app.MakeSiteAsync();
        await ThemesOf(site).UseAsync(new ThemeKey.Saved(await ThemesOf(site).AddAsync("Charcoal", Charcoal)));

        var admin = await GetAsync(site, PageUrls.AdminDashboard);
        var platformHome = await app.ClientFor(TestApp.PlatformHost).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(SiteThemeSelector, admin);
        Assert.DoesNotContain("#202020", admin);
        Assert.DoesNotContain(SiteThemeSelector, platformHome);
    }

    [Fact]
    public async Task ThePageOpensOnTheThemeInUse()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);
        await ThemesOf(site).UseAsync(new ThemeKey.Saved(id));

        var page = await GetAsync(site, PageUrls.Theme);

        Assert.Contains("[data-theme-preview] { --theme-page: #202020;", page);
        Assert.Contains("Currently set theme: <strong>Charcoal</strong>", page);
        Assert.DoesNotContain("Use this theme", page);
    }

    [Fact]
    public async Task AnUnknownThemeIsNotFound()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await client.GetAsync(PageUrls.ThemeEditing(new ThemeKey.Saved(new ThemeId(999999))), TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    // picking is all the artist did
    [Fact]
    public async Task PickingAColorPreviewsItWithoutSaving()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(site, PaperFields((fields, role) => fields[$"{role(ColorRole.Page)}.Color"] = "#202020"));

        Assert.Contains("[data-theme-preview] { --theme-page: #202020;", page);
        Assert.Contains("Not saved yet.", page);
        Assert.Empty(await ThemesOf(site).GetAllAsync());
    }

    // a preset can't change, so its only save is a copy
    [Fact]
    public async Task OnlyTheWebsitesOwnThemesCanBeSavedOver()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var preset = await GetAsync(site, PageUrls.ThemeEditing(new ThemeKey.Preset(ThemePreset.Dark)));
        var own = await GetAsync(site, PageUrls.ThemeEditing(new ThemeKey.Saved(id)));

        Assert.DoesNotContain("name=\"Input.Save\"", preset);
        Assert.Contains("name=\"Input.Save\"", own);
    }

    // a preset can't be renamed, so only the Save as new theme dialog names it
    [Fact]
    public async Task OnlyTheWebsitesOwnThemesHaveANameToChange()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var preset = await GetAsync(site, PageUrls.ThemeEditing(new ThemeKey.Preset(ThemePreset.Dark)));
        var own = await GetAsync(site, PageUrls.ThemeEditing(new ThemeKey.Saved(id)));

        Assert.Null(InputValue(preset, "Input.Name"));
        Assert.Equal("Dark copy", InputValue(preset, "Input.NewName"));
        Assert.Equal("Charcoal", InputValue(own, "Input.Name"));
        Assert.Equal("Charcoal copy", InputValue(own, "Input.NewName"));
    }

    [Fact]
    public async Task SaveAsNewThemeKeepsACopyWithoutUsingIt()
    {
        var site = await app.MakeSiteAsync();

        await PostThemeAsync(
            site,
            Fields(ThemePresets.Paper.Theme, "Evening", (fields, role) =>
            {
                fields[$"{role(ColorRole.Accent)}.Color"] = "#aa3300";
                fields[$"{role(ColorRole.Backdrop)}.OpacityPercent"] = "70";
                fields["Input.SaveAsNew"] = "true";
            })
        );

        var saved = Assert.Single(await ThemesOf(site).GetAllAsync());
        Assert.Equal("Evening", saved.Name);
        Assert.Equal(RgbColor.Parse("#aa3300"), saved.Theme.Colors.ChosenFor(ColorRole.Accent));
        Assert.Equal(RgbColor.Black.WithOpacityPercent(70), saved.Theme.Colors.ChosenFor(ColorRole.Backdrop));
        // a copy: what Paper chose is chosen here too, and what it derived stays derived
        Assert.Equal(Paper.ChosenFor(ColorRole.Bar), saved.Theme.Colors.ChosenFor(ColorRole.Bar));
        Assert.Null(saved.Theme.Colors.ChosenFor(ColorRole.Panel));
        Assert.Equal(new ThemeKey.Preset(ThemePreset.Paper), (await ThemesOf(site).GetInUseAsync())?.Key);
    }

    // Enter in a field presses its form's first submit button in the page, counting those outside it
    // that join it through form=, like the switch dialog's Save and switch
    [Fact]
    public async Task EnterInTheNameSaves()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var page = await GetAsync(site, PageUrls.ThemeEditing(new ThemeKey.Saved(id)));

        var formStart = page.IndexOf("id=\"theme-form\"", StringComparison.Ordinal);
        var formEnd = page.IndexOf("</form>", formStart, StringComparison.Ordinal);
        var first = Regex.Matches(page, "<button [^>]*type=\"submit\"[^>]*>")
            .First(button =>
                (button.Index > formStart && button.Index < formEnd) || button.Value.Contains("form=\"theme-form\"", StringComparison.Ordinal)
            );

        Assert.Contains("name=\"Input.Save\"", first.Value);
    }

    [Fact]
    public async Task SaveAndUsePutsTheNewThemeOnTheWebsite()
    {
        var site = await app.MakeSiteAsync();

        await PostThemeAsync(site, Fields(ThemePresets.Paper.Theme, "Evening", (fields, _) => fields["Input.SaveAsNewAndUse"] = "true"));

        var saved = Assert.Single(await ThemesOf(site).GetAllAsync());
        Assert.Equal("Evening", saved.Name);
        Assert.Equal(new ThemeKey.Saved(saved.Id), (await ThemesOf(site).GetInUseAsync())?.Key);
    }

    // the switch dialog's Save and switch, sent when another theme was chosen with changes not saved
    [Fact]
    public async Task SaveAndSwitchSavesThenOpensTheThemeChosen()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var dark = new ThemeKey.Preset(ThemePreset.Dark);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.ThemeEditing(new ThemeKey.Saved(id)),
            "theme",
            Fields(Charcoal, "Charcoal", (fields, role) =>
            {
                fields[$"{role(ColorRole.Page)}.Color"] = "#101010";
                fields["Input.SwitchTo"] = dark.QueryValue;
            })
        );

        Assert.Equal(RgbColor.Parse("#101010"), Assert.Single(await ThemesOf(site).GetAllAsync()).Theme.Colors.ChosenFor(ColorRole.Page));
        Assert.Equal(PageUrls.ThemeEditing(dark), response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task SaveKeepsTheChangesAndTheNameInTheOpenTheme()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        await PostThemeAsync(
            site,
            Fields(Charcoal, "Darker", (fields, role) =>
            {
                fields[$"{role(ColorRole.Page)}.Color"] = "#101010";
                fields["Input.Save"] = "true";
            }),
            PageUrls.ThemeEditing(new ThemeKey.Saved(id))
        );

        var saved = Assert.Single(await ThemesOf(site).GetAllAsync());
        Assert.Equal("Darker", saved.Name);
        Assert.Equal(RgbColor.Parse("#101010"), saved.Theme.Colors.ChosenFor(ColorRole.Page));
    }

    [Fact]
    public async Task ANameAnotherThemeHasIsRefused()
    {
        var site = await app.MakeSiteAsync();
        await ThemesOf(site).AddAsync("Evening", Charcoal);

        var page = await PostThemeAsync(site, Fields(ThemePresets.Paper.Theme, "evening", (fields, _) => fields["Input.SaveAsNew"] = "true"));

        Assert.Contains("Another theme is already called", page);
        Assert.Single(await ThemesOf(site).GetAllAsync());
    }

    [Fact]
    public async Task SavingNeedsAName()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(site, Fields(ThemePresets.Paper.Theme, " ", (fields, _) => fields["Input.SaveAsNew"] = "true"));

        Assert.Contains("Give the theme a name.", page);
        Assert.Empty(await ThemesOf(site).GetAllAsync());
    }

    // a reset previews like a pick, and Save keeps it
    [Fact]
    public async Task ResetPutsARoleBackAsTheOpenThemeHasItSaved()
    {
        var site = await app.MakeSiteAsync();
        var id = await ThemesOf(site).AddAsync("Charcoal", Charcoal);

        var page = await PostThemeAsync(
            site,
            Fields(Charcoal, "Charcoal", (fields, role) =>
            {
                fields[$"{role(ColorRole.Bar)}.Color"] = "#00aa00";
                fields[$"{role(ColorRole.Link)}.Color"] = "#00aa00";
                fields["Input.Reset"] = nameof(ColorRole.Bar);
            }),
            PageUrls.ThemeEditing(new ThemeKey.Saved(id))
        );

        Assert.Contains("--theme-bar: #aa0000;", page);
        Assert.Contains("--theme-link: #00aa00;", page);
    }

    [Fact]
    public async Task DeriveAllDerivesEveryRoleThatCanBe()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(site, PaperFields((fields, _) => fields["Input.DeriveAll"] = "true"));

        // Paper's #f5f5f5 page, 6% of the way to its black text
        Assert.Contains("[data-theme-preview] { --theme-page: #f5f5f5; --theme-ink: #000000; --theme-accent: #155dfc; --theme-bar: #e6e6e6;", page);
        Assert.DoesNotContain("Derive all from base colors", page);
    }

    [Fact]
    public async Task UseThisThemePutsItOnTheWebsite()
    {
        var site = await app.MakeSiteAsync();
        var key = new ThemeKey.Saved(await ThemesOf(site).AddAsync("Charcoal", Charcoal));

        await PostAsync(site, PageUrls.ThemeEditing(key), "use-theme", []);

        Assert.Equal(key, (await ThemesOf(site).GetInUseAsync())?.Key);
    }

    [Fact]
    public async Task DeletingTheThemeInUseWarnsThenPutsTheWebsiteBackOnPaper()
    {
        var site = await app.MakeSiteAsync();
        var key = new ThemeKey.Saved(await ThemesOf(site).AddAsync("Charcoal", Charcoal));
        await ThemesOf(site).UseAsync(key);

        var page = await GetAsync(site, PageUrls.ThemeEditing(key));
        await PostAsync(site, PageUrls.ThemeEditing(key), "delete-theme", []);

        Assert.Contains("so it will go back to Paper", page);
        Assert.Empty(await ThemesOf(site).GetAllAsync());
        Assert.Equal(new ThemeKey.Preset(ThemePreset.Paper), (await ThemesOf(site).GetInUseAsync())?.Key);
    }

    [Fact]
    public async Task ColorsHardToReadAreWarnedOf()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(site, PaperFields((fields, role) => fields[$"{role(ColorRole.InkFaded)}.Color"] = "#eeeeee"));

        Assert.Contains("Hard to read on background", page);
    }

    [Fact]
    public async Task APostMissingARoleIsRefused()
    {
        var site = await app.MakeSiteAsync();

        var page = await PostThemeAsync(
            site,
            PaperFields((fields, _) =>
            {
                foreach (var key in fields.Keys.Where(key => key.StartsWith("Input.Roles[14]", StringComparison.Ordinal)).ToList())
                {
                    fields.Remove(key);
                }

                fields["Input.SaveAsNew"] = "true";
            })
        );

        Assert.Contains("don&#x27;t match this page", page);
        Assert.Empty(await ThemesOf(site).GetAllAsync());
    }
}
