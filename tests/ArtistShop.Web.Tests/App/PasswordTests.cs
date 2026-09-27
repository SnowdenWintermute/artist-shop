using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace ArtistShop.Web.Tests.App;

// A password changes only by a link emailed to the account's address: from the account page's Password
// box when signed in, or Forgot password when not
[Collection(TestAppCollection.Name)]
public sealed partial class PasswordTests(TestApp app)
{
    [Fact]
    public async Task ThePasswordBoxEmailsALinkThatChangesItAndSignsOutEverywhere()
    {
        var email = await app.MakeAccountAsync();
        var here = await app.SignedInClientAsync(TestApp.PlatformHost, email);
        var elsewhere = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        var response = await TestApp.PostFormAsync(here, "/Account/Manage", "email-password-link", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Equal("Reset your password", sent.Subject);

        await ResetAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(sent.HtmlBody), email, "Another-password-2");

        Assert.False(await IsSignedInAsync(here));
        Assert.False(await IsSignedInAsync(elsewhere));
        var signIn = await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/Login",
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = "Another-password-2" }
        );
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
    }

    // an account made by signing in with Google starts with no password
    [Fact]
    public async Task AGoogleOnlyAccountGetsItsFirstPasswordByTheLink()
    {
        var email = $"{Guid.NewGuid():n}@example.com";
        var client = app.ClientFor(TestApp.PlatformHost);
        await TestApp.SignInWithGoogleAsync(client, new FakeGoogleLogin(email, Guid.NewGuid().ToString("n"), EmailVerified: true));

        await TestApp.PostFormAsync(client, "/Account/Manage", "email-password-link", []);
        await ResetAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(Assert.Single(app.Mailer.SentTo(email)).HtmlBody), email, "Another-password-2");

        var signIn = await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/Login",
            "login",
            new() { ["Input.Email"] = email, ["Input.Password"] = "Another-password-2" }
        );
        // a wrong password answers with the form again
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
    }

    [Fact]
    public async Task ForgotPasswordEmailsTheSameLink()
    {
        var email = await app.MakeAccountAsync();

        await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/ForgotPassword",
            "forgot-password",
            new() { ["Input.Email"] = email }
        );

        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Contains($"http://{TestApp.PlatformHost}/Account/ResetPassword?code=", WebUtility.HtmlDecode(sent.HtmlBody));
    }

    [Fact]
    public async Task OverTheEmailLimitForgotPasswordSendsNothingButAnswersTheSame()
    {
        var email = await app.MakeAccountAsync();
        app.EmailSendLimit.Refuse(email);

        var response = await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/ForgotPassword",
            "forgot-password",
            new() { ["Input.Email"] = email }
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/ForgotPasswordConfirmation", response.Headers.Location?.AbsolutePath);
        Assert.Empty(app.Mailer.SentTo(email));
    }

    // the account's owner knows their own address, so the box says plainly
    [Fact]
    public async Task OverTheEmailLimitThePasswordBoxSendsNothingAndSaysSo()
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);
        app.EmailSendLimit.Refuse(email);

        await TestApp.PostFormAsync(client, "/Account/Manage", "email-password-link", []);

        Assert.Empty(app.Mailer.SentTo(email));
        Assert.Contains("Try again in a minute.", await client.GetStringAsync("/Account/Manage", TestContext.Current.CancellationToken));
    }

    // changing a password or an email from a signed-in browser alone isn't possible, and the account
    // page's tabs are one page now
    [Theory]
    [InlineData("/Account/Manage/ChangePassword")]
    [InlineData("/Account/Manage/SetPassword")]
    [InlineData("/Account/Manage/Email")]
    [InlineData("/Account/ConfirmEmailChange")]
    [InlineData("/Account/Manage/Password")]
    [InlineData("/Account/Manage/ExternalLogins")]
    [InlineData("/Account/Manage/PersonalData")]
    [InlineData("/Account/Manage/DeletePersonalData")]
    public async Task TheOldPagesAreGone(string path)
    {
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync());

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string link, string email, string password) =>
        TestApp.PostFormAsync(
            client,
            link,
            "reset-password",
            new()
            {
                ["Input.Email"] = email,
                ["Input.Code"] = CodeIn(link),
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password,
            }
        );

    // the page puts the decoded code in a hidden field, which PostFormAsync's fields replace
    private static string CodeIn(string link) =>
        Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(QueryHelpers.ParseQuery(new Uri(link).Query)["code"].ToString()));

    private static async Task<bool> IsSignedInAsync(HttpClient client) =>
        (await client.GetAsync("/sites", TestContext.Current.CancellationToken)).StatusCode is HttpStatusCode.OK;

    // an email's one link, as the reader's mail program would follow it
    private static string LinkIn(string htmlBody) => WebUtility.HtmlDecode(Link().Match(htmlBody).Groups["link"].Value);

    [GeneratedRegex("href=\"(?<link>[^\"]+)\"")]
    private static partial Regex Link();
}
