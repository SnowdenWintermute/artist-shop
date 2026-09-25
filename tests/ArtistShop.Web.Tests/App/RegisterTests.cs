using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The platform's register page, which must answer the same whether an email has an account or not
[Collection(TestAppCollection.Name)]
public sealed partial class RegisterTests(TestApp app)
{
    [Fact]
    public async Task ANewEmailGetsAnAccountToConfirmByItsLink()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email, TestApp.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/RegisterConfirmation", response.Headers.Location?.AbsolutePath);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Equal("Confirm your email", sent.Subject);
        Assert.False((await FindAsync(email))?.EmailConfirmed);

        var confirmation = await app.ClientFor(TestApp.PlatformHost).GetStringAsync(LinkIn(sent.HtmlBody), TestContext.Current.CancellationToken);

        Assert.Contains("Thank you for confirming your email.", confirmation);
        Assert.True((await FindAsync(email))?.EmailConfirmed);
    }

    // the page gives the same answer; only the email, which goes to the address's owner, differs
    [Fact]
    public async Task AnEmailWithAnAccountGetsTheSameAnswerAndAnEmailSayingSo()
    {
        var email = await app.MakeAccountAsync();

        var response = await RegisterAsync(email, "Another-password-2");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/RegisterConfirmation", response.Headers.Location?.AbsolutePath);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Equal("You already have an account", sent.Subject);
        Assert.Contains($"http://{TestApp.PlatformHost}/Account/ForgotPassword", sent.HtmlBody);
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
        Assert.True(await userManager.CheckPasswordAsync(account, TestApp.Password));
    }

    // checked before the email is looked up, so the answer doesn't say whether it has an account
    [Fact]
    public async Task AWeakPasswordGetsTheSameAnswerForEitherEmail()
    {
        var newEmail = NewEmail();
        var existingEmail = await app.MakeAccountAsync();

        var forNew = await ReadAsync(await RegisterAsync(newEmail, "weak"));
        var forExisting = await ReadAsync(await RegisterAsync(existingEmail, "weak"));

        Assert.Contains("Passwords must be at least 6 characters.", forNew);
        Assert.Contains("Passwords must be at least 6 characters.", forExisting);
        Assert.Empty(app.Mailer.SentTo(newEmail));
        Assert.Empty(app.Mailer.SentTo(existingEmail));
        Assert.Null(await FindAsync(newEmail));
    }

    [Fact]
    public async Task PasswordsThatDontMatchAreRefused()
    {
        var response = await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/Register",
            "register",
            new()
            {
                ["Input.Email"] = NewEmail(),
                ["Input.Password"] = TestApp.Password,
                ["Input.ConfirmPassword"] = "Something-else-1",
            }
        );

        Assert.Contains("The passwords don&#x27;t match.", await ReadAsync(response));
    }

    // accounts are made on the platform, for now
    [Fact]
    public async Task TheRegisterPageIsNotFoundOnASite()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync("/Account/Register", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            "/Account/Register",
            "register",
            new()
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password,
            }
        );

    private async Task<ApplicationUser?> FindAsync(string email)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private static Task<string> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // the email's one link, as the reader's mail program would follow it
    private static string LinkIn(string htmlBody) =>
        WebUtility.HtmlDecode(Link().Match(htmlBody).Groups["link"].Value);

    private static string NewEmail() => $"{Guid.NewGuid():n}@example.com";

    [GeneratedRegex("href=\"(?<link>[^\"]+)\"")]
    private static partial Regex Link();
}
