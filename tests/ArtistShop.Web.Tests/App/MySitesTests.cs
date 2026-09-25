using System.Net;

namespace ArtistShop.Web.Tests.App;

// The platform's "My websites" page, which lists the websites the signed-in account is a member of
[Collection(TestAppCollection.Name)]
public sealed class MySitesTests(TestApp app)
{
    // each website's address links to its home, and Manage to its admin, which asks anyone not
    // signed in on that website to sign in there first
    [Fact]
    public async Task AnOwnersWebsiteIsListedWithItsHomeAndAdmin()
    {
        var page = await ReadAsync(await app.SignedInClientAsync(TestApp.PlatformHost, TestApp.FirstSiteOwnerEmail));

        Assert.Contains($"href=\"http://{TestApp.FirstSiteHost}/\"", page);
        Assert.Contains($"href=\"http://{TestApp.FirstSiteHost}/admin\"", page);
        Assert.Contains("Owner", page);
    }

    [Fact]
    public async Task AnAccountWithNoWebsitesIsToldSo()
    {
        var page = await ReadAsync(await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync()));

        Assert.Contains("You don't have a website yet.", page);
        Assert.DoesNotContain(TestApp.FirstSiteHost, page);
    }

    [Fact]
    public async Task SomeoneNotSignedInIsSentToSignIn()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync("/sites", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ThePageIsNotFoundOnASite()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync("/sites", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string> ReadAsync(HttpClient client)
    {
        var response = await client.GetAsync("/sites", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
