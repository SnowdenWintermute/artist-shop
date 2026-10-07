using System.Net;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Sign-ins kept in the database (DatabaseTicketStore), which end at once when their row goes or the
// account's security stamp changes. "Signed in" here means My websites loads rather than sending the
// client to sign in
[Collection(TestAppCollection.Name)]
public sealed class SessionTests(TestApp app)
{
    [Fact]
    public async Task ASignInIsARowAndTheCookieOnlyItsKey()
    {
        var email = await app.MakeAccountAsync();

        var (client, cookie) = await app.SignInWithCookieAsync(TestApp.PlatformHost, email);

        Assert.True(await IsSignedInAsync(client));
        Assert.Equal(1, await SessionCountAsync(email));
        Assert.True(cookie.Length < 400, $"The cookie is {cookie.Length} characters.");
    }

    // what a stolen cookie is worth once its owner signs out
    [Fact]
    public async Task SigningOutEndsEveryCopyOfTheCookie()
    {
        var email = await app.MakeAccountAsync();
        var (client, cookie) = await app.SignInWithCookieAsync(TestApp.PlatformHost, email);
        var copy = app.ClientWithCookie(TestApp.PlatformHost, cookie);
        Assert.True(await IsSignedInAsync(copy));

        await TestApp.SignOutAsync(client);

        Assert.False(await IsSignedInAsync(copy));
        Assert.Equal(0, await SessionCountAsync(email));
    }

    // Removing a Google login renews the stamp, and the page signs its own browser in again, which
    // renews its row rather than making another, so that row must take the new stamp
    [Fact]
    public async Task AChangeThatRenewsTheStampKeepsThisBrowserSignedIn()
    {
        var email = await app.MakeAccountAsync();
        var googleId = Guid.NewGuid().ToString("n");
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
            Assert.True((await userManager.AddLoginAsync(account, new UserLoginInfo("Google", googleId, "Google"))).Succeeded);
        }
        var here = await app.SignedInClientAsync(TestApp.PlatformHost, email);
        var elsewhere = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        await TestApp.PostFormAsync(
            here,
            "/Account/Manage",
            "remove-login-Google",
            new() { ["LoginProvider"] = "Google", ["ProviderKey"] = googleId }
        );

        Assert.True(await IsSignedInAsync(here));
        Assert.False(await IsSignedInAsync(elsewhere));
    }

    // The cookie handler renews the sign-in a request came with, so signing in over another account's
    // would keep that account's row under the cookie's key. It ends that sign-in and makes a new one
    [Fact]
    public async Task SigningInAsAnotherAccountReplacesTheSignIn()
    {
        var first = await app.MakeAccountAsync();
        var second = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, first);

        await TestApp.PostFormAsync(
            client,
            "/Account/Login",
            "login",
            new() { ["Input.Email"] = second, ["Input.Password"] = TestApp.Password }
        );

        var home = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains(second, home);
        Assert.DoesNotContain(first, home);
        Assert.Equal(0, await SessionCountAsync(first));
        Assert.Equal(1, await SessionCountAsync(second));
    }

    [Fact]
    public async Task ResettingThePasswordEndsEverySignIn()
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
            var token = await userManager.GeneratePasswordResetTokenAsync(account);
            Assert.True((await userManager.ResetPasswordAsync(account, token, "Another-password-2")).Succeeded);
        }

        Assert.False(await IsSignedInAsync(client));
    }

    // each host has its own cookie, so its own sign-in; a new stamp ends them all
    [Fact]
    public async Task ANewSecurityStampEndsSignInsOnEveryHost()
    {
        var site = await app.MakeSiteAsync();
        var onPlatform = await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail);
        var onSite = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await UpdateStampAsync(site.OwnerEmail);

        Assert.False(await IsSignedInAsync(onPlatform));
        var response = await onSite.GetAsync("/admin", TestContext.Current.CancellationToken);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task DeletingTheAccountDeletesItsSignIns()
    {
        var email = await app.MakeAccountAsync();
        await app.SignedInClientAsync(TestApp.PlatformHost, email);
        await app.SignedInClientAsync(TestApp.PlatformHost, email);
        Assert.Equal(2, await SessionCountAsync(email));
        var userId = await app.UserIdAsync(email);

        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await userManager.DeleteAsync(await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("No account."))).Succeeded);
        }

        using var check = app.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserSessions.AnyAsync(session => session.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnExpiredSignInIsRefused()
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        await ExpireSessionsAsync(email);

        Assert.False(await IsSignedInAsync(client));
    }

    [Fact]
    public async Task TheCleanupDeletesExpiredAndStaleSignInsOnly()
    {
        var expired = await app.MakeAccountAsync();
        var stale = await app.MakeAccountAsync();
        var live = await app.MakeAccountAsync();
        foreach (var email in new[] { expired, stale, live })
        {
            await app.SignedInClientAsync(TestApp.PlatformHost, email);
        }
        await ExpireSessionsAsync(expired);
        await UpdateStampAsync(stale);

        await app.Services.GetRequiredService<DatabaseTicketStore>().DeleteEndedAsync();

        Assert.Equal(0, await SessionCountAsync(expired));
        Assert.Equal(0, await SessionCountAsync(stale));
        Assert.Equal(1, await SessionCountAsync(live));
    }

    // the operator's Sign everyone out
    [Fact]
    public async Task EndingAllSignInsSignsOutEveryAccountOnEveryHost()
    {
        var site = await app.MakeSiteAsync();
        var other = await app.MakeAccountAsync();
        var onPlatform = await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail);
        var onSite = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var otherOnPlatform = await app.SignedInClientAsync(TestApp.PlatformHost, other);

        await app.Services.GetRequiredService<DatabaseTicketStore>().EndAllAsync();

        Assert.False(await IsSignedInAsync(onPlatform));
        Assert.False(await IsSignedInAsync(otherOnPlatform));
        var response = await onSite.GetAsync("/admin", TestContext.Current.CancellationToken);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    private static async Task<bool> IsSignedInAsync(HttpClient client)
    {
        var response = await client.GetAsync("/sites", TestContext.Current.CancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.Redirect when response.Headers.Location?.AbsolutePath == "/Account/Login" => false,
            var status => throw new InvalidOperationException($"My websites answered {status}."),
        };
    }

    private async Task<int> SessionCountAsync(string email)
    {
        var userId = await app.UserIdAsync(email);
        using var scope = app.Services.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .UserSessions.CountAsync(session => session.UserId == userId, TestContext.Current.CancellationToken);
    }

    private async Task ExpireSessionsAsync(string email)
    {
        var userId = await app.UserIdAsync(email);
        using var scope = app.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .UserSessions.Where(session => session.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken
            );
    }

    private async Task UpdateStampAsync(string email)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
        Assert.True((await userManager.UpdateSecurityStampAsync(account)).Succeeded);
    }
}
