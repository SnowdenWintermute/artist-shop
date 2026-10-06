using System.Net;

namespace ArtistShop.Web.Tests.App;

// An owner deleting their website from My websites, and keeping it again. Each test on a site of its
// own, since the other tests need the first site online
[Collection(TestAppCollection.Name)]
public sealed class SiteDeletionTests(TestApp app)
{
    // the address in any case, as a host is
    [Fact]
    public async Task DeletingTakesTheWebsiteOffline()
    {
        var site = await app.MakeSiteAsync();
        var client = await OwnerClientAsync(site);

        var response = await DeleteAsync(client, site, site.Host.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sites", response.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.NotFound, await HomeStatusAsync(site));
        Assert.Contains("deleting on", await ReadAsync(client, "/sites"));
    }

    // the owner with where to keep it, and each admin with who deleted it
    [Fact]
    public async Task DeletingEmailsTheWebsitesMembers()
    {
        var site = await app.MakeSiteAsync();
        var admin = await app.MakeAdminAsync(site.Id);

        await DeleteAsync(await OwnerClientAsync(site), site, site.Host);

        Assert.Contains(
            $"href=\"http://{TestApp.PlatformHost}/sites\"",
            Assert.Single(app.Mailer.SentTo(site.OwnerEmail)).HtmlBody
        );
        Assert.Contains(site.OwnerEmail, Assert.Single(app.Mailer.SentTo(admin)).HtmlBody);
    }

    [Fact]
    public async Task AWrongAddressDeletesNothing()
    {
        var site = await app.MakeSiteAsync();

        var response = await DeleteAsync(await OwnerClientAsync(site), site, TestApp.FirstSiteHost);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "This isn&#x27;t the website&#x27;s address.",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(HttpStatusCode.OK, await HomeStatusAsync(site));
    }

    // on the website's own host, since it goes offline with the website
    [Fact]
    public async Task TheDeletePageLinksToTheWebsitesExport()
    {
        var site = await app.MakeSiteAsync();

        Assert.Contains($"http://{site.Host}/admin/export", await ReadAsync(await OwnerClientAsync(site), DeletePath(site)));
    }

    // the delete page is on the platform; an admin can't delete, so isn't shown it
    [Fact]
    public async Task TheDashboardLinksOnlyTheOwnerToTheDeletePage()
    {
        var site = await app.MakeSiteAsync();
        var owner = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var admin = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var ownersPage = await ReadAsync(owner, "/admin");
        Assert.Contains("Delete website", ownersPage);
        Assert.Contains($"href=\"http://{TestApp.PlatformHost}{DeletePath(site)}\"", ownersPage);
        Assert.DoesNotContain("Delete website", await ReadAsync(admin, "/admin"));
    }

    // not its admins, and not a site already being deleted
    [Fact]
    public async Task OnlyTheOwnerOfAWebsiteOnlineFindsItsDeletePage()
    {
        var site = await app.MakeSiteAsync();
        var admin = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAdminAsync(site.Id));
        var owner = await OwnerClientAsync(site);

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(admin, DeletePath(site)));

        await DeleteAsync(owner, site, site.Host);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(owner, DeletePath(site)));
    }

    [Fact]
    public async Task KeepingPutsTheWebsiteBackOnline()
    {
        var site = await app.MakeSiteAsync();
        var client = await OwnerClientAsync(site);
        await DeleteAsync(client, site, site.Host);

        var response = await TestApp.PostFormAsync(client, "/sites", $"keep-site-{site.Id.Value}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await HomeStatusAsync(site));
        Assert.DoesNotContain("deleting on", await ReadAsync(client, "/sites"));
    }

    private Task<HttpClient> OwnerClientAsync(TestSite site) => app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail);

    private static string DeletePath(TestSite site) => $"/sites/{site.Id.Value}/delete";

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, TestSite site, string address) =>
        TestApp.PostFormAsync(client, DeletePath(site), "delete-site", new() { ["Input.Address"] = address });

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
