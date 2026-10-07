using System.Net;
using ArtistShop.Web.Database.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The operator's Websites page: every website, the home page's examples, and deleting one outright.
// Each test on a site of its own, since the other tests need the first site online
[Collection(TestAppCollection.Name)]
public sealed class OperatorSitesTests(TestApp app)
{
    private const string SitesPath = "/operator/sites";

    [Fact]
    public async Task ListsEveryWebsiteWithItsOwner()
    {
        var site = await app.MakeSiteAsync();

        var page = await ReadAsync(await OperatorClientAsync(), SitesPath);

        Assert.Contains(site.Host, page);
        Assert.Contains(site.OwnerEmail, page);
    }

    [Fact]
    public async Task IsTheOperatorsOnly()
    {
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync());

        var response = await client.GetAsync(SitesPath, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // each change of the checkbox flips it
    [Fact]
    public async Task CheckingExampleListsTheWebsiteOnTheHomePage()
    {
        var site = await app.MakeSiteAsync();
        var client = await OperatorClientAsync();
        var homeLink = $"href=\"http://{site.Host}/\"";

        await ToggleExampleAsync(client, site);
        Assert.Contains(homeLink, await ReadAsync(app.ClientFor(TestApp.PlatformHost), "/"));

        await ToggleExampleAsync(client, site);
        Assert.DoesNotContain(homeLink, await ReadAsync(app.ClientFor(TestApp.PlatformHost), "/"));
    }

    [Fact]
    public async Task DeletingErasesTheWebsiteAtOnceAndEmailsTheOwner()
    {
        var site = await app.MakeSiteAsync();

        var response = await DeleteAsync(site, site.Host.ToUpperInvariant(), emailOwner: true, "Closed for <spam>.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SitesPath, response.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.NotFound, await HomeStatusAsync(site));
        Assert.DoesNotContain(site.Id, await app.Services.GetRequiredService<SiteRepository>().GetIdsAsync());
        Assert.Contains("<p>Closed for &lt;spam&gt;.</p>", Assert.Single(app.Mailer.SentTo(site.OwnerEmail)).HtmlBody);
    }

    [Fact]
    public async Task DeletingWithEmailUncheckedEmailsNobody()
    {
        var site = await app.MakeSiteAsync();

        await DeleteAsync(site, site.Host, emailOwner: false, "Unsent.");

        Assert.Equal(HttpStatusCode.NotFound, await HomeStatusAsync(site));
        Assert.Empty(app.Mailer.SentTo(site.OwnerEmail));
    }

    [Fact]
    public async Task AWrongAddressDeletesNothing()
    {
        var site = await app.MakeSiteAsync();

        var response = await DeleteAsync(site, TestApp.FirstSiteHost, emailOwner: true, "Gone.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await HomeStatusAsync(site));
        Assert.Empty(app.Mailer.SentTo(site.OwnerEmail));
    }

    private Task<HttpClient> OperatorClientAsync() => app.SignedInClientAsync(TestApp.PlatformHost, TestApp.OperatorEmail);

    private static async Task ToggleExampleAsync(HttpClient client, TestSite site)
    {
        var response = await TestApp.PostFormAsync(client, SitesPath, $"example-site-{site.Id.Value}", []);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    // an unchecked checkbox sends nothing, as a browser's does
    private async Task<HttpResponseMessage> DeleteAsync(TestSite site, string address, bool emailOwner, string message)
    {
        var fields = new Dictionary<string, string> { ["Input.Address"] = address, ["Input.Message"] = message };

        if (emailOwner)
        {
            fields["Input.EmailOwner"] = "true";
        }

        return await TestApp.PostFormAsync(
            await OperatorClientAsync(),
            $"{SitesPath}/{site.Id.Value}/delete",
            "operator-delete-site",
            fields
        );
    }

    private Task<HttpStatusCode> HomeStatusAsync(TestSite site) => StatusAsync(app.ClientFor(site.Host), "/");

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string path) =>
        (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode;

    private static async Task<string> ReadAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
