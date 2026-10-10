using System.Net;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

[Collection(TestAppCollection.Name)]
public sealed class OperatorFontsTests(TestApp app)
{
    [Fact]
    public async Task ListsTheFontsInTheOrderSaved()
    {
        await app.Services.GetRequiredService<FontRepository>().ReorderAsync([Font.Pacifico, Font.Roboto]);

        var page = await ReadAsync(await OperatorClientAsync(), PageUrls.OperatorFonts);

        var pacifico = page.IndexOf(">Pacifico<", StringComparison.Ordinal);
        var roboto = page.IndexOf(">Roboto<", StringComparison.Ordinal);
        var bitter = page.IndexOf(">Bitter<", StringComparison.Ordinal);
        Assert.True(pacifico >= 0 && pacifico < roboto && roboto < bitter);
    }

    // the quotes stay quotes: inside <style> the browser doesn't decode &quot;
    [Fact]
    public async Task DeclaresEachFontsFiles()
    {
        var page = await ReadAsync(await OperatorClientAsync(), PageUrls.OperatorFonts);

        Assert.Matches(@"@font-face \{ font-family: ""Pacifico""; src: url\(""fonts/pacifico/Pacifico-Regular\.\w+\.woff2""\)", page);
    }

    [Fact]
    public async Task IsTheOperatorsOnly()
    {
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync());

        var response = await client.GetAsync(PageUrls.OperatorFonts, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryFontFileIsServed()
    {
        var client = app.ClientFor(TestApp.PlatformHost);

        foreach (var font in Fonts.All)
        {
            foreach (var face in font.Faces)
            {
                var response = await client.GetAsync($"/{font.AssetPath(face)}", TestContext.Current.CancellationToken);
                Assert.True(response.StatusCode == HttpStatusCode.OK, font.AssetPath(face));
            }
        }
    }

    private Task<HttpClient> OperatorClientAsync() => app.SignedInClientAsync(TestApp.PlatformHost, TestApp.OperatorEmail);

    private static async Task<string> ReadAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
