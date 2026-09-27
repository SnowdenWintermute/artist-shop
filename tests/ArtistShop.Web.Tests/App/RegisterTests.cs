using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The platform's register page, which must answer the same whether an email has an account or not,
// and the page its emailed link opens, which makes the account
[Collection(TestAppCollection.Name)]
public sealed partial class RegisterTests(TestApp app)
{
    [Fact]
    public async Task ANewEmailGetsALinkAndNoAccountUntilItsUsed()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/RegisterConfirmation", response.Headers.Location?.AbsolutePath);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Equal("Finish making your account", sent.Subject);
        Assert.Null(await FindAsync(email));
    }

    [Fact]
    public async Task TheLinkMakesAConfirmedAccountAndSignsIn()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var client = app.ClientFor(TestApp.PlatformHost);

        var response = await ChoosePasswordAsync(client, LinkIn(email), TestApp.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sites", response.Headers.Location?.PathAndQuery);
        var account = await FindAsync(email) ?? throw new InvalidOperationException("No account.");
        Assert.True(account.EmailConfirmed);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/sites", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task MakingAnAccountWhileSignedInAsAnotherSignsInToTheNewOne()
    {
        var other = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, other);
        var email = NewEmail();
        await RegisterAsync(email);

        await ChoosePasswordAsync(client, LinkIn(email), TestApp.Password);

        var home = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains(email, home);
        Assert.DoesNotContain(other, home);
    }

    // Made before accounts waited for their email: signing in and resetting the password refuse it,
    // and whoever chose its password never proved the address. Registering sends the same link as for
    // a new address, which replaces that password
    [Fact]
    public async Task AnUnconfirmedAccountGetsALinkThatReplacesItsPassword()
    {
        var email = await app.MakeUnconfirmedAccountAsync();

        await RegisterAsync(email);
        var response = await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(email), "Another-password-2");

        Assert.Equal("/sites", response.Headers.Location?.PathAndQuery);
        using var check = app.Services.CreateScope();
        var userManager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var account = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
        Assert.True(account.EmailConfirmed);
        Assert.True(await userManager.CheckPasswordAsync(account, "Another-password-2"));
        Assert.False(await userManager.CheckPasswordAsync(account, TestApp.Password));
    }

    [Fact]
    public async Task AWeakPasswordLeavesAnUnconfirmedAccountAsItWas()
    {
        var email = await app.MakeUnconfirmedAccountAsync();
        await RegisterAsync(email);

        var response = await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(email), "weak");

        Assert.Contains("Passwords must be at least 6 characters.", await ReadAsync(response));
        Assert.False((await FindAsync(email))?.EmailConfirmed);
    }

    // the page that sent them to sign in, carried from Login through Register and the email
    [Fact]
    public async Task TheLinkGoesBackToThePageThatSentThemToSignIn()
    {
        var email = NewEmail();
        await RegisterAsync(email, "/operator?tab=codes");

        var response = await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(email), TestApp.Password);

        Assert.Equal("/operator?tab=codes", response.Headers.Location?.PathAndQuery);
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    [InlineData("/\\evil.test/")]
    public async Task AReturnAddressOnAnotherHostIsIgnored(string returnUrl)
    {
        var email = NewEmail();
        await RegisterAsync(email, returnUrl);

        var response = await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(email), TestApp.Password);

        Assert.Equal("/sites", response.Headers.Location?.PathAndQuery);
    }

    // The attack this design exists for: someone registers an address they can't read, hoping its
    // owner confirms the account later with the stranger's password still on it. No account is made
    // until the link is used, so there's nothing for the stranger's password to be on
    [Fact]
    public async Task RegisteringSomeoneElsesAddressLeavesNothingBehind()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await RegisterAsync(email);

        var sent = app.Mailer.SentTo(email);
        Assert.Equal(["Finish making your account", "Finish making your account"], sent.Select(message => message.Subject));
        Assert.Null(await FindAsync(email));

        await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkInBody(sent[1].HtmlBody), TestApp.Password);

        Assert.True((await FindAsync(email))?.EmailConfirmed);
    }

    [Fact]
    public async Task ALinkUsedAlreadySaysTheAccountExists()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var link = LinkIn(email);
        await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), link, TestApp.Password);

        var page = await app.ClientFor(TestApp.PlatformHost).GetStringAsync(link, TestContext.Current.CancellationToken);

        Assert.Contains("You already have an account", page);
        Assert.DoesNotContain("Password again", page);
    }

    [Fact]
    public async Task AnAlteredLinkIsRefused()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var page = await app.ClientFor(TestApp.PlatformHost).GetStringAsync(LinkIn(email) + "x", TestContext.Current.CancellationToken);

        Assert.Contains("This link doesn't work", page);
    }

    [Fact]
    public async Task AWeakPasswordIsRefusedAndMakesNoAccount()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await ChoosePasswordAsync(app.ClientFor(TestApp.PlatformHost), LinkIn(email), "weak");

        Assert.Contains("Passwords must be at least 6 characters.", await ReadAsync(response));
        Assert.Null(await FindAsync(email));
    }

    [Fact]
    public async Task PasswordsThatDontMatchAreRefused()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            LinkIn(email),
            "choose-password",
            new() { ["Input.Password"] = TestApp.Password, ["Input.ConfirmPassword"] = "Something-else-1" }
        );

        Assert.Contains("The passwords don&#x27;t match.", await ReadAsync(response));
        Assert.Null(await FindAsync(email));
    }

    // the page gives the same answer; only the email, which goes to the address's owner, differs
    [Fact]
    public async Task AnEmailWithAnAccountGetsTheSameAnswerAndAnEmailSayingSo()
    {
        var email = await app.MakeAccountAsync();

        var response = await RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/RegisterConfirmation", response.Headers.Location?.AbsolutePath);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Equal("You already have an account", sent.Subject);
        Assert.Contains($"http://{TestApp.PlatformHost}/Account/ForgotPassword", sent.HtmlBody);
    }

    // accounts are made on the platform, for now
    [Theory]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ChoosePassword")]
    public async Task TheRegisterPagesAreNotFoundOnASite(string path)
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string? returnUrl = null) =>
        TestApp.PostFormAsync(
            app.ClientFor(TestApp.PlatformHost),
            returnUrl is null ? "/Account/Register" : $"/Account/Register?returnUrl={Uri.EscapeDataString(returnUrl)}",
            "register",
            new() { ["Input.Email"] = email }
        );

    private static Task<HttpResponseMessage> ChoosePasswordAsync(HttpClient client, string link, string password) =>
        TestApp.PostFormAsync(
            client,
            link,
            "choose-password",
            new() { ["Input.Password"] = password, ["Input.ConfirmPassword"] = password }
        );

    private async Task<ApplicationUser?> FindAsync(string email)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private static Task<string> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // the link in the one email sent to the address
    private string LinkIn(string email) => LinkInBody(Assert.Single(app.Mailer.SentTo(email)).HtmlBody);

    // an email's one link, as the reader's mail program would follow it
    private static string LinkInBody(string htmlBody) =>
        WebUtility.HtmlDecode(Link().Match(htmlBody).Groups["link"].Value);

    private static string NewEmail() => $"{Guid.NewGuid():n}@example.com";

    [GeneratedRegex("href=\"(?<link>[^\"]+)\"")]
    private static partial Regex Link();
}
