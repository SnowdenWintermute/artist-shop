using System.Net;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The platform's /signup form, posted by a signed-in account as a browser would
[Collection(TestAppCollection.Name)]
public sealed class SignUpTests(TestApp app)
{
    [Fact]
    public async Task SigningUpMakesASiteForTheSignedInAccountAndSendsItToMyWebsites()
    {
        var email = await app.MakeAccountAsync();
        var name = NewName();

        var response = await SignUpAsync(email, await app.MakeSignUpCodeAsync(), name);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sites", response.Headers.Location?.AbsolutePath);
        var home = await app.ClientFor($"{name}.{TestApp.PlatformHost}").GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal(SiteRole.Owner, await RoleAsync(name, email));
    }

    // an owner making a second site
    [Fact]
    public async Task AnAccountCanOwnMoreThanOneSite()
    {
        var email = await app.MakeAccountAsync();
        var first = NewName();
        var second = NewName();

        await SignUpAsync(email, await app.MakeSignUpCodeAsync(), first);
        await SignUpAsync(email, await app.MakeSignUpCodeAsync(), second);

        Assert.Equal(SiteRole.Owner, await RoleAsync(first, email));
        Assert.Equal(SiteRole.Owner, await RoleAsync(second, email));
    }

    [Fact]
    public async Task ACodeWorksOnce()
    {
        var email = await app.MakeAccountAsync();
        var code = await app.MakeSignUpCodeAsync();
        await SignUpAsync(email, code, NewName());

        var response = await SignUpAsync(email, code, NewName());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This code has expired or has already been used.", await ReadAsync(response));
    }

    // the code is only used if the site is made, so it works with another name
    [Fact]
    public async Task ATakenNameIsRefusedAndTheCodeKept()
    {
        var email = await app.MakeAccountAsync();
        var name = NewName();
        await SignUpAsync(email, await app.MakeSignUpCodeAsync(), name);
        var code = await app.MakeSignUpCodeAsync();

        var refused = await SignUpAsync(email, code, name);
        var retried = await SignUpAsync(email, code, NewName());

        Assert.Contains("Another website has this name.", await ReadAsync(refused));
        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
    }

    [Fact]
    public async Task ANameTheRulesRefuseIsShownOnTheForm()
    {
        var response = await SignUpAsync(await app.MakeAccountAsync(), await app.MakeSignUpCodeAsync(), "www");

        Assert.Contains("This name is reserved.", await ReadAsync(response));
    }

    [Fact]
    public async Task SomeoneNotSignedInIsSentToSignIn()
    {
        var response = await app.ClientFor(TestApp.PlatformHost).GetAsync("/signup", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task TheSignUpPageIsNotFoundOnASite()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync("/signup", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SignUpAsync(string email, SignUpCode code, string name) =>
        await TestApp.PostFormAsync(
            await app.SignedInClientAsync(TestApp.PlatformHost, email),
            "/signup",
            "sign-up",
            new() { ["Input.Code"] = code.Text, ["Input.Name"] = name }
        );

    private static Task<string> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // the role the email's account has on the site the name made
    private async Task<SiteRole?> RoleAsync(string name, string email)
    {
        using var scope = app.Services.CreateScope();
        var account =
            await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"{email} has no account.");
        var site = Assert.IsType<CurrentHost.Site>(
            app.Services.GetRequiredService<HostDirectory>().Find($"{name}.{TestApp.PlatformHost}")
        );

        return await app.Services.GetRequiredService<SiteRepository>().GetMemberRoleAsync(site.Id, account.Id);
    }

    // unique to one test, since the tests share the app's databases
    private static string NewName() => $"s{Guid.NewGuid():n}"[..20];
}
