using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using NetVips;

namespace ArtistShop.Web.Tests.App;

// The logos and browser tab icon set on the Branding page, as visitors' browsers get them
[Collection(TestAppCollection.Name)]
public sealed partial class BrandingTests(TestApp app)
{
    private static byte[] Png(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return image.WriteToBuffer(".png");
    }

    // the page's sections are FormMappingScopes named by BrandImageType, each with an "upload" form
    private async Task<HttpResponseMessage> UploadAsync(TestSite site, string type, byte[] bytes, string fileName, string contentType)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        return await TestApp.PostFileAsync(
            client,
            PageUrls.Branding,
            PageUrls.Branding,
            "Upload.File",
            fileName,
            contentType,
            bytes,
            new() { ["_handler"] = $"[{type}]upload" }
        );
    }

    // decoded, since Blazor writes the + in image/svg+xml as &#x2B;
    private async Task<string> HomeAsync(TestSite site) =>
        WebUtility.HtmlDecode(await app.ClientFor(site.Host).GetStringAsync("/", TestContext.Current.CancellationToken));

    [Fact]
    public async Task AnUploadedLogoIsTheNavsLinkHomeAndIsServedForAYear()
    {
        var site = await app.MakeSiteAsync();

        await UploadAsync(site, "Logo", Png(300, 100), "logo.png", "image/png");

        var home = await HomeAsync(site);
        var src = BrandImageSource().Match(home).Groups["src"].Value;
        Assert.Matches(@"^/brand/[0-9a-f]{64}\.webp$", src);
        Assert.Contains($"alt=\"{site.Host.Split('.')[0]}\"", home);

        var response = await app.ClientFor(site.Host).GetAsync(src, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", string.Join(", ", response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task AnotherWebsitesHostDoesntServeTheFile()
    {
        var site = await app.MakeSiteAsync();
        var otherSite = await app.MakeSiteAsync();
        await UploadAsync(site, "Logo", Png(300, 100), "logo.png", "image/png");
        var src = BrandImageSource().Match(await HomeAsync(site)).Groups["src"].Value;

        var response = await app.ClientFor(otherSite.Host).GetAsync(src, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithBothLogosTheThemeChoosesWhichShows()
    {
        var site = await app.MakeSiteAsync();

        await UploadAsync(site, "Logo", Png(300, 100), "light.png", "image/png");
        await UploadAsync(site, "LogoOnDark", Png(310, 100), "dark.png", "image/png");

        var home = await HomeAsync(site);
        Assert.Contains("[display:var(--theme-logo-display)]", home);
        Assert.Contains("[display:var(--theme-logo-on-dark-display)]", home);
        // Paper's bar is light
        Assert.Contains("--theme-logo-display: block; --theme-logo-on-dark-display: none;", home);
    }

    [Fact]
    public async Task WithOnlyADarkLogoItShowsEverywhere()
    {
        var site = await app.MakeSiteAsync();

        await UploadAsync(site, "LogoOnDark", Png(320, 100), "dark.png", "image/png");

        var home = await HomeAsync(site);
        Assert.Single(BrandImageSource().Matches(home));
        Assert.DoesNotContain("[display:var(--theme-logo", home);
    }

    [Fact]
    public async Task ARefusedImageSaysWhyAndChangesNothing()
    {
        var site = await app.MakeSiteAsync();

        var response = await UploadAsync(site, "Logo", Png(300, 20), "thin.png", "image/png");

        Assert.Contains("Logos must be at least 45 pixels high.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotMatch(BrandImageSource(), await HomeAsync(site));
    }

    [Fact]
    public async Task RemovingTheLogoBringsBackTheLetters()
    {
        var site = await app.MakeSiteAsync();
        await UploadAsync(site, "Logo", Png(300, 100), "logo.png", "image/png");
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, PageUrls.Branding, "[Logo]remove", []);

        var home = await HomeAsync(site);
        Assert.DoesNotMatch(BrandImageSource(), home);
        Assert.Contains($">{site.Host[..2].ToUpperInvariant()}</div>", home);
    }

    [Fact]
    public async Task AnUploadedIconGivesBrowsersAndPhonesTheirFiles()
    {
        var site = await app.MakeSiteAsync();
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><circle cx="32" cy="32" r="30"/></svg>""";

        await UploadAsync(site, "Favicon", Encoding.UTF8.GetBytes(svg), "icon.svg", "image/svg+xml");

        var home = await HomeAsync(site);
        Assert.Matches("""<link rel="icon" type="image/png" sizes="32x32" href="/brand/[0-9a-f]{64}\.png" />""", home);
        Assert.Matches("""<link rel="icon" type="image/svg\+xml" href="/brand/[0-9a-f]{64}\.svg" />""", home);
        Assert.Matches("""<link rel="apple-touch-icon" href="/brand/[0-9a-f]{64}\.png" />""", home);
    }

    [Fact]
    public async Task WithoutAnIconTheTabShowsPictureCordsIcon()
    {
        var site = await app.MakeSiteAsync();

        var home = await HomeAsync(site);

        Assert.Matches("<link rel=\"icon\" [^>]*href=\"favicon.svg\"", home);
        Assert.DoesNotContain("/brand/", home);
    }

    [GeneratedRegex("""<img src="(?<src>/brand/[^"]+)" """)]
    private static partial Regex BrandImageSource();
}
