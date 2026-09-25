using System.Net;
using ArtistShop.Web.Publishing;

namespace ArtistShop.Web.Tests.App;

// Which host a request is for decides what answers it: UseKnownHosts, then UseServedOnHosts, then
// sign-in. Program.cs's order matters, so these go through the whole app
[Collection(TestAppCollection.Name)]
public sealed class HostRoutingTests(TestApp app)
{
    [Fact]
    public async Task AnUnknownHostGetsABare404()
    {
        var response = await app.ClientFor("unknown.test").GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnAddressGetsABare404()
    {
        var response = await app.ClientFor("127.0.0.1").GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThePlatformsHomeIsThePlatformsOwn()
    {
        var page = await app.ClientFor(TestApp.PlatformHost).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<h1 class=\"mb-4 text-2xl\">Artist websites</h1>", page);
    }

    [Fact]
    public async Task ASitesHomeIsTheSites()
    {
        var page = await app.ClientFor(TestApp.FirstSiteHost).GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("<title>Home</title>", page);
        Assert.DoesNotContain("Artist websites", page);
    }

    // the blog is a site's page, so the platform shows its not-found page
    [Fact]
    public async Task ASitesPageIsNotFoundOnThePlatform()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync("/posts", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // a 404 rather than a redirect to sign in: the host is checked before sign-in is
    [Fact]
    public async Task ThePlatformsPageIsNotFoundOnASite()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync("/operator", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not Found", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThePlatformsOperatorPageAsksSomeoneNotSignedInToSignIn()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync("/operator", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ASitesAdminAsksSomeoneNotSignedInToSignIn()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // the platform isn't a site, so its admin pages don't exist there
    [Fact]
    public async Task ASitesAdminIsNotFoundOnThePlatform()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // an endpoint rather than a page, marked ServedOn(Site) as the variants and uploads are
    [Fact]
    public async Task ASiteOnlyEndpointIsNotFoundOnThePlatform()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync(VideoLinkEndpoints.Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // a 401 rather than the pages' redirect: since .NET 10, sign-in answers an API endpoint that way
    [Fact]
    public async Task ASiteOnlyEndpointTurnsAwaySomeoneNotSignedInOnASite()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync(VideoLinkEndpoints.Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
