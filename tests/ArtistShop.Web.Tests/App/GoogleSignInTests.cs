using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Google sign-in: on the platform, whose /signin-google is the address registered with Google and
// where websites send their sign-ins, putting Google's login on the account with the email it has
// verified. Google itself isn't reached: FakeGoogle leaves the external cookie as Google's handler
// does, then the tests follow the app's callback, /Account/ExternalLogin
[Collection(TestAppCollection.Name)]
public sealed partial class GoogleSignInTests(TestApp app)
{
    [Fact]
    public async Task ThePlatformsSignInSendsToGoogleWithItsOwnCallback()
    {
        var client = app.ClientFor(TestApp.PlatformHost);
        var login = await client.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        Assert.Contains("value=\"Google\"", login);

        var response = await client.PostAsync(
            "/Account/PerformExternalLogin",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["provider"] = "Google",
                    ["returnUrl"] = "/sites",
                    ["__RequestVerificationToken"] = AntiforgeryToken().Match(login).Groups["token"].Value,
                }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location ?? throw new InvalidOperationException("No redirect.");
        Assert.Equal("accounts.google.com", location.Host);
        Assert.Equal($"http://{TestApp.PlatformHost}/signin-google", QueryHelpers.ParseQuery(location.Query)["redirect_uri"]);
    }

    // a website's sign-in happens on the platform, Google's included
    [Fact]
    public async Task AWebsiteDoesntSendToGoogleItself()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).PostAsync(
            "/Account/PerformExternalLogin",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["provider"] = "Google", ["returnUrl"] = "/" }),
            TestContext.Current.CancellationToken
        );

        // refused as a 404, which the status code pages run again as a POST to /not-found, where it
        // fails antiforgery: a 400. Either way, never a redirect to Google
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ANewVerifiedEmailGetsAConfirmedAccountWithNoPassword()
    {
        var email = NewEmail();

        var result = await AddLoginAsync(GoogleLogin(email, verified: true));

        Assert.IsType<ExternalAccountResult.Added>(result);
        var (account, hasPassword, providers) = await ReadAsync(email);
        Assert.True(account.EmailConfirmed);
        Assert.False(hasPassword);
        Assert.Equal(["Google"], providers);
    }

    [Fact]
    public async Task AnUnverifiedEmailGetsNoAccount()
    {
        var email = NewEmail();

        var result = await AddLoginAsync(GoogleLogin(email, verified: false));

        Assert.IsType<ExternalAccountResult.NoVerifiedEmail>(result);
        using var scope = app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task AnAccountWithTheEmailGetsTheLoginAndKeepsItsPassword()
    {
        var email = await app.MakeAccountAsync();

        await AddLoginAsync(GoogleLogin(email.ToUpperInvariant(), verified: true));

        var (_, hasPassword, providers) = await ReadAsync(email);
        Assert.True(hasPassword);
        Assert.Equal(["Google"], providers);
    }

    // made before accounts waited for their email: its password could be anyone's
    [Fact]
    public async Task AnUnconfirmedAccountLosesItsPasswordAndIsConfirmed()
    {
        var email = await app.MakeUnconfirmedAccountAsync();

        await AddLoginAsync(GoogleLogin(email, verified: true));

        var (account, hasPassword, providers) = await ReadAsync(email);
        Assert.True(account.EmailConfirmed);
        Assert.False(hasPassword);
        Assert.Equal(["Google"], providers);
    }

    [Fact]
    public async Task TheCallbackSignsANewVerifiedEmailInToANewAccount()
    {
        var email = NewEmail();
        var client = app.ClientFor(TestApp.PlatformHost);

        var response = await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(email, NewGoogleId(), EmailVerified: true));

        Assert.Equal("/sites", response.Headers.Location?.AbsolutePath);
        Assert.Contains(email, await HomeAsync(client));
        Assert.True((await ReadAsync(email)).Account.EmailConfirmed);
    }

    // the account is found by Google's id for the login from then on, not the address
    [Fact]
    public async Task ALoginOnAnAccountSignsInToItWhateverEmailGoogleGives()
    {
        var email = await app.MakeAccountAsync();
        var googleId = NewGoogleId();
        await AddLoginAsync(GoogleLogin(email, verified: true, googleId));
        var client = app.ClientFor(TestApp.PlatformHost);

        await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(NewEmail(), googleId, EmailVerified: false));

        Assert.Contains(email, await HomeAsync(client));
    }

    [Fact]
    public async Task TheCallbackSendsAnUnverifiedEmailBackToLogin()
    {
        var email = NewEmail();
        var client = app.ClientFor(TestApp.PlatformHost);

        var response = await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(email, NewGoogleId(), EmailVerified: false));

        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain(email, await HomeAsync(client));
        using var scope = app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task ALockedOutAccountGoesToLockout()
    {
        var email = await app.MakeAccountAsync();
        var googleId = NewGoogleId();
        await AddLoginAsync(GoogleLogin(email, verified: true, googleId));
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
            Assert.True((await userManager.SetLockoutEndDateAsync(account, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        }
        var client = app.ClientFor(TestApp.PlatformHost);

        var response = await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(email, googleId, EmailVerified: true));

        Assert.Equal("/Account/Lockout", response.Headers.Location?.AbsolutePath);
        Assert.DoesNotContain(email, await HomeAsync(client));
    }

    // with no password, the Google login is the only way in
    [Fact]
    public async Task TheOnlyWayInCantBeRemoved()
    {
        var email = NewEmail();
        var googleId = NewGoogleId();
        var client = app.ClientFor(TestApp.PlatformHost);
        await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(email, googleId, EmailVerified: true));

        var page = await client.GetStringAsync("/Account/Manage", TestContext.Current.CancellationToken);
        await TestApp.PostFormAsync(
            client,
            "/Account/Manage",
            "remove-login-Google",
            new() { ["LoginProvider"] = "Google", ["ProviderKey"] = googleId }
        );

        Assert.Contains("To remove it, first give the account a password.", page);
        Assert.Equal(["Google"], (await ReadAsync(email)).Providers);
    }

    [Theory]
    [InlineData("""{"email_verified": true}""", "true")]
    [InlineData("""{"verified_email": true}""", "true")]
    [InlineData("""{"email_verified": false}""", "false")]
    [InlineData("""{}""", null)]
    public void GooglesVerifiedFlagIsReadInEitherSpelling(string json, string? expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, ExternalAccounts.ReadGoogleEmailVerified(document.RootElement));
    }

    private async Task<ExternalAccountResult> AddLoginAsync(ExternalLoginInfo login)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalAccounts>().AddLoginAsync(login);
    }

    private async Task<(ApplicationUser Account, bool HasPassword, List<string> Providers)> ReadAsync(string email)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
        var logins = await userManager.GetLoginsAsync(account);
        return (account, await userManager.HasPasswordAsync(account), [.. logins.Select(login => login.LoginProvider)]);
    }

    // the menu names whoever is signed in
    private static Task<string> HomeAsync(HttpClient client) =>
        client.GetStringAsync($"http://{TestApp.PlatformHost}/", TestContext.Current.CancellationToken);

    // as Google's handler leaves it in the external cookie, with a new Google account id unless given one
    private static ExternalLoginInfo GoogleLogin(string email, bool verified, string? googleId = null) =>
        new(
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ExternalAccounts.EmailVerifiedClaimType, verified ? "true" : "false"),
                    ],
                    "Google"
                )
            ),
            "Google",
            googleId ?? NewGoogleId(),
            "Google"
        );

    private static string NewGoogleId() => Guid.NewGuid().ToString("n");

    private static string NewEmail() => $"{Guid.NewGuid():n}@example.com";

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
