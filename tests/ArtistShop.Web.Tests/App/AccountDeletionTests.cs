using System.Net;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Deleting an account from its page's Delete account box, on the platform, since an account is the
// whole platform's. Owners are on sites of their own, since the other tests need the first site online
[Collection(TestAppCollection.Name)]
public sealed class AccountDeletionTests(TestApp app)
{
    private const string Path = "/Account/Manage";

    [Fact]
    public async Task ThePageSaysTheAccountIsThePlatforms()
    {
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, await app.MakeAccountAsync());

        var page = await (await client.GetAsync(Path, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );

        Assert.Contains("This is your PictureCord account.", page);
    }

    // nobody can export an owned website once the account is gone
    [Fact]
    public async Task ThePageLinksToEachOwnedWebsitesExport()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail);

        var page = await (await client.GetAsync(Path, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );

        Assert.Contains($"http://{site.Host}/admin/export", page);
    }

    [Fact]
    public async Task DeletingAnAccountWithNoWebsitesDeletesOnlyTheAccount()
    {
        var email = await app.MakeAccountAsync();
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        var response = await DeleteAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"http://{TestApp.PlatformHost}/", response.Headers.Location?.AbsoluteUri);
        Assert.False(await HasAccountAsync(email));
        Assert.Contains("Your PictureCord account was deleted", Assert.Single(app.Mailer.SentTo(email)).Subject);
    }

    // they hang off the platform profile, which goes with the account
    [Fact]
    public async Task DeletingAnAccountDeletesItsDismissedHints()
    {
        var email = await app.MakeAccountAsync();
        var userId = await app.UserIdAsync(email);
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);
        var dismissedHints = app.Services.GetRequiredService<DismissedHintRepository>();
        await dismissedHints.DismissAsync(userId, [HintType.Series]);
        Assert.NotEmpty(await dismissedHints.GetAsync(userId));

        await DeleteAsync(client, email);

        Assert.Empty(await dismissedHints.GetAsync(userId));
    }

    [Fact]
    public async Task DeletingAnOwnersAccountTakesTheirWebsiteOfflineAndTellsItsAdmins()
    {
        var site = await app.MakeSiteAsync();
        var admin = await app.MakeAdminAsync(site.Id);

        await DeleteAsync(await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail), site.OwnerEmail);

        Assert.False(await HasAccountAsync(site.OwnerEmail));
        Assert.Equal(HttpStatusCode.NotFound, await HomeStatusAsync(site.Host));
        var toAdmin = Assert.Single(app.Mailer.SentTo(admin)).HtmlBody;
        Assert.Contains(site.OwnerEmail, toAdmin);
        Assert.Contains("deleted their account", toAdmin);
        Assert.Contains(site.Host, Assert.Single(app.Mailer.SentTo(site.OwnerEmail)).HtmlBody);
    }

    // once the account is gone, so no member row names an account that doesn't exist; the website
    // keeps its admins, with their emails, until it's erased
    [Fact]
    public async Task DeletingAnOwnersAccountLeavesTheirWebsiteWithNoOwner()
    {
        var site = await app.MakeSiteAsync();
        var admin = await app.MakeAdminAsync(site.Id);

        await DeleteAsync(await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail), site.OwnerEmail);

        using var scope = app.Services.CreateScope();
        var member = Assert.Single(await scope.ServiceProvider.GetRequiredService<SiteMemberAccounts>().GetAsync(site.Id));
        Assert.Equal((admin, SiteRole.Admin), (member.Email.Value, member.Role));
    }

    // their memberships of other websites go, and those websites stay online
    [Fact]
    public async Task DeletingAnAdminsAccountRemovesThemFromTheWebsitesTheyHelpRun()
    {
        var email = await app.MakeFirstSiteAdminAsync();
        var userId = await app.UserIdAsync(email);

        await DeleteAsync(await app.SignedInClientAsync(TestApp.PlatformHost, email), email);

        Assert.Null(await app.Services.GetRequiredService<SiteRepository>().GetMemberRoleAsync(app.FirstSiteId, userId));
        Assert.Equal(HttpStatusCode.OK, await HomeStatusAsync(TestApp.FirstSiteHost));
        Assert.Contains(TestApp.FirstSiteHost, Assert.Single(app.Mailer.SentTo(email)).HtmlBody);
    }

    // a website's account link goes to the platform's
    [Fact]
    public async Task ThePageIsNotFoundOnAWebsite()
    {
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, await app.MakeAccountAsync());

        var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // typing the email is the confirmation; no password is asked, so an account with none can go too
    [Fact]
    public async Task TheWrongEmailDeletesNothing()
    {
        var site = await app.MakeSiteAsync();

        await DeleteAsync(await app.SignedInClientAsync(TestApp.PlatformHost, site.OwnerEmail), "someone-else@example.com");

        Assert.True(await HasAccountAsync(site.OwnerEmail));
        Assert.Equal(HttpStatusCode.OK, await HomeStatusAsync(site.Host));
        Assert.Empty(app.Mailer.SentTo(site.OwnerEmail));
    }

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string typedEmail) =>
        TestApp.PostFormAsync(client, Path, "delete-account", new() { ["TypedEmail"] = typedEmail });

    private async Task<HttpStatusCode> HomeStatusAsync(string host) =>
        (await app.ClientFor(host).GetAsync("/", TestContext.Current.CancellationToken)).StatusCode;

    private async Task<bool> HasAccountAsync(string email)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
            is not null;
    }
}
