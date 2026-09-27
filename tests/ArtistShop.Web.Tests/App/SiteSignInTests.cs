using System.Net;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Signing in on a website through the platform (SiteSignIns): the website's Login sends the browser to
// the platform's SignInTo, which signs in there if need be and sends it back to the website's Handoff
// with a single-use code
[Collection(TestAppCollection.Name)]
public sealed class SiteSignInTests(TestApp app)
{
    private const string Handoff = "Account/Handoff";

    [Fact]
    public async Task AWebsitesAdminSendsThroughThePlatformsSignInAndBack()
    {
        var email = await app.MakeFirstSiteAdminAsync();
        var client = app.ClientFor(TestApp.FirstSiteHost);

        var toLogin = await client.GetAsync("/admin", TestContext.Current.CancellationToken);
        var toPlatform = await client.GetAsync(Location(toLogin), TestContext.Current.CancellationToken);
        Assert.Equal($"http://{TestApp.PlatformHost}/Account/SignInTo", Location(toPlatform).GetLeftPart(UriPartial.Path));
        var toPlatformLogin = await client.GetAsync(Location(toPlatform), TestContext.Current.CancellationToken);
        Assert.Equal("/Account/Login", Location(toPlatformLogin).AbsolutePath);

        var signedIn = await TestApp.PostFormAsync(
            client,
            Location(toPlatformLogin).AbsoluteUri,
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = TestApp.Password }
        );
        var toHandoff = await client.GetAsync(Location(signedIn), TestContext.Current.CancellationToken);
        var landed = await client.GetAsync(Location(toHandoff), TestContext.Current.CancellationToken);

        Assert.Equal("/admin", Location(landed).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Contains(email, await client.GetStringAsync("/", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SignedInOnThePlatformItsOneTripWithNoForm()
    {
        var client = await app.SignedInOnPlatformClientAsync(TestApp.FirstSiteHost, await app.MakeFirstSiteAdminAsync());

        var landed = await TestApp.FollowSiteSignInAsync(client, "/Account/Login?ReturnUrl=%2Fadmin");

        Assert.Equal("/admin", Location(landed).PathAndQuery);
    }

    [Fact]
    public async Task ACodeWorksOnce()
    {
        var (_, cookie, handoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);

        var first = await app.ClientWithCookie(TestApp.FirstSiteHost, cookie).GetAsync(handoff, TestContext.Current.CancellationToken);
        var second = await app.ClientWithCookie(TestApp.FirstSiteHost, cookie).GetAsync(handoff, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        await AssertRefusedAsync(second);
    }

    // Login CSRF: someone makes a code for their own account and gets another browser to open its
    // link. That browser's cookie holds its own nonce, not theirs, so it isn't signed in as them
    [Fact]
    public async Task AnotherBrowsersCodeIsRefused()
    {
        var (theirEmail, _, theirHandoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);
        var victim = app.ClientFor(TestApp.FirstSiteHost);
        await victim.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        var response = await victim.GetAsync(theirHandoff, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response);
        // the menu names whoever is signed in
        Assert.DoesNotContain(theirEmail, await victim.GetStringAsync("/", TestContext.Current.CancellationToken));
    }

    // even with the same browser's cookie, a code made for one website doesn't sign in on another
    [Fact]
    public async Task ACodeForAnotherWebsiteIsRefused()
    {
        var other = await app.MakeSiteAsync();
        var (_, cookie, handoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);

        var response = await app.ClientWithCookie(other.Host, cookie).GetAsync(
            new Uri($"http://{other.Host}{handoff.PathAndQuery}"),
            TestContext.Current.CancellationToken
        );

        await AssertRefusedAsync(response);
    }

    [Fact]
    public async Task AnExpiredCodeIsRefused()
    {
        var (_, cookie, handoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);
        using (var scope = app.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .SiteSignInCodes.ExecuteUpdateAsync(
                    setters => setters.SetProperty(code => code.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)),
                    TestContext.Current.CancellationToken
                );
        }

        var response = await app.ClientWithCookie(TestApp.FirstSiteHost, cookie).GetAsync(handoff, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response);
    }

    // so a code only ever goes to one of the app's own websites
    [Theory]
    [InlineData("evil.test")]
    [InlineData(TestApp.PlatformHost)]
    public async Task ThePlatformMakesNoCodeForAHostThatIsntAWebsites(string site)
    {
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync());

        var response = await client.GetAsync($"/Account/SignInTo?site={site}&nonce=anything", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    public async Task AReturnAddressOnAnotherHostIsIgnored(string returnUrl)
    {
        var client = await app.SignedInOnPlatformClientAsync(TestApp.FirstSiteHost, await app.MakeAccountAsync());

        var landed = await TestApp.FollowSiteSignInAsync(client, $"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");

        Assert.Equal("/", Location(landed).PathAndQuery);
    }

    // "remember me" on the platform's sign-in, so closing the browser keeps the website's too
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RememberMeCarriesToTheWebsite(bool rememberMe)
    {
        var client = app.ClientFor(TestApp.FirstSiteHost);
        await TestApp.PostFormAsync(
            client,
            $"http://{TestApp.PlatformHost}/Account/Login",
            "login",
            new()
            {
                ["Input.Email"] = await app.MakeAccountAsync(),
                ["Input.Password"] = TestApp.Password,
                ["Input.RememberMe"] = rememberMe ? "true" : "false",
            }
        );

        var landed = await TestApp.FollowSiteSignInAsync(client, "/Account/Login");

        var cookie = landed
            .Headers.GetValues("Set-Cookie")
            .Single(header => header.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Equal(rememberMe, cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    // Logging out on any host ends every sign-in in the browser: the platform's, which each website's
    // was handed over from, and so every website's
    [Theory]
    [InlineData(TestApp.FirstSiteHost)]
    [InlineData(TestApp.PlatformHost)]
    public async Task LoggingOutAnywhereEndsEverySignInInTheBrowser(string logOutOn)
    {
        var other = await app.MakeSiteAsync();
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, email);
        await TestApp.FollowSiteSignInAsync(client, $"http://{other.Host}/Account/Login");
        Assert.True(await IsSignedInAsync(client, other.Host, email));

        await TestApp.SignOutAsync(client, logOutOn);

        Assert.False(await IsSignedInAsync(client, TestApp.PlatformHost, email));
        Assert.False(await IsSignedInAsync(client, TestApp.FirstSiteHost, email));
        Assert.False(await IsSignedInAsync(client, other.Host, email));
        Assert.Equal(0, await SessionCountAsync(email));
    }

    // the browser's own sign-ins only: ending another device's is what a new security stamp does
    [Fact]
    public async Task LoggingOutLeavesAnotherBrowserSignedIn()
    {
        var email = await app.MakeAccountAsync();
        var here = await app.SignedInClientAsync(TestApp.FirstSiteHost, email);
        var elsewhere = await app.SignedInClientAsync(TestApp.FirstSiteHost, email);

        await TestApp.SignOutAsync(here);

        Assert.True(await IsSignedInAsync(elsewhere, TestApp.FirstSiteHost, email));
        Assert.True(await IsSignedInAsync(elsewhere, TestApp.PlatformHost, email));
    }

    // A website in use keeps its platform sign-in, whose row it lives by, from expiring first. Here
    // the platform's sign-in, row and ticket both, would have expired, then the website's is renewed
    // as its cookie handler does while it's used
    [Fact]
    public async Task AWebsiteInUseKeepsThePlatformsSignIn()
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, email);
        var userId = await app.UserIdAsync(email);
        using var scope = app.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var platform = await database.UserSessions.SingleAsync(
            session => session.UserId == userId && session.ParentId == null,
            TestContext.Current.CancellationToken
        );
        var platformTicket = Deserialize(platform.Ticket);
        platformTicket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        platform.Ticket = TicketSerializer.Default.Serialize(platformTicket);
        platform.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var website = await database.UserSessions.AsNoTracking().SingleAsync(
            session => session.ParentId == platform.Id,
            TestContext.Current.CancellationToken
        );
        var websiteTicket = Deserialize(website.Ticket);
        websiteTicket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);

        await app.Services.GetRequiredService<DatabaseTicketStore>().RenewAsync(website.Id, websiteTicket);

        // the platform first: ending its sign-in there would take the website's with it
        Assert.True(await IsSignedInAsync(client, TestApp.PlatformHost, email));
        Assert.True(await IsSignedInAsync(client, TestApp.FirstSiteHost, email));
    }

    // Logged out on the platform between its code being made and the website using it: the website
    // doesn't sign in, rather than failing, or outliving the sign-in it was handed over from
    [Fact]
    public async Task AHandoffAfterThePlatformsSignInWasDeletedIsRefused()
    {
        var (email, cookie, handoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);
        var userId = await app.UserIdAsync(email);
        using (var scope = app.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .UserSessions.Where(session => session.UserId == userId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        var response = await app.ClientWithCookie(TestApp.FirstSiteHost, cookie).GetAsync(handoff, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response);
    }

    // as when the password was reset meanwhile, which ends the platform's sign-in but leaves its row
    [Fact]
    public async Task AHandoffAfterANewSecurityStampIsRefused()
    {
        var (email, cookie, handoff) = await StartAndGetHandoffAsync(TestApp.FirstSiteHost);
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
            Assert.True((await userManager.UpdateSecurityStampAsync(account)).Succeeded);
        }

        var response = await app.ClientWithCookie(TestApp.FirstSiteHost, cookie).GetAsync(handoff, TestContext.Current.CancellationToken);

        await AssertRefusedAsync(response);
    }

    private static AuthenticationTicket Deserialize(byte[] ticket) =>
        TicketSerializer.Default.Deserialize(ticket) ?? throw new InvalidOperationException("No ticket.");

    // the menu names whoever is signed in; the platform's and every website's show it
    private static async Task<bool> IsSignedInAsync(HttpClient client, string host, string email) =>
        (await client.GetStringAsync($"http://{host}/", TestContext.Current.CancellationToken)).Contains(email, StringComparison.Ordinal);

    private async Task<int> SessionCountAsync(string email)
    {
        var userId = await app.UserIdAsync(email);
        using var scope = app.Services.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .UserSessions.CountAsync(session => session.UserId == userId, TestContext.Current.CancellationToken);
    }

    // A new account signed in on the platform, starting a sign-in on the website without finishing it:
    // its email, the website's cookie as "name=value", and the handoff address the platform sent back
    private async Task<(string Email, string Cookie, Uri Handoff)> StartAndGetHandoffAsync(string host)
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInOnPlatformClientAsync(host, email);

        var start = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);
        var cookie = start
            .Headers.GetValues("Set-Cookie")
            .Single(header => header.StartsWith("ArtistShop.SiteSignIn=", StringComparison.Ordinal))
            .Split(';')[0];
        var toHandoff = await client.GetAsync(Location(start), TestContext.Current.CancellationToken);
        var handoff = Location(toHandoff);
        Assert.Equal($"http://{host}/{Handoff}", handoff.GetLeftPart(UriPartial.Path));

        return (email, cookie, handoff);
    }

    // The handoff's page saying it didn't work, rather than a redirect back signed in. The text alone
    // isn't enough: a redirect's body could carry anything
    private static async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Signing in didn&#x27;t work", await ReadAsync(response));
    }

    private static Uri Location(HttpResponseMessage response) =>
        response.Headers.Location ?? throw new InvalidOperationException($"No redirect: {response.StatusCode}.");

    private static Task<string> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
