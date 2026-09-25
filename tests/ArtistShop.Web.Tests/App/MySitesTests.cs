using System.Net;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using Microsoft.Extensions.DependencyInjection;

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

    // listed for that email only, until it expires
    [Fact]
    public async Task AnInvitationIsListedForItsEmailOnly()
    {
        var invited = await app.MakeAccountAsync();
        var expired = await app.MakeAccountAsync();
        await InviteAsync(invited, DateTimeOffset.UtcNow.AddDays(1));
        await InviteAsync(expired, DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Contains($"accept-invite-{app.FirstSiteId.Value}", await ReadAsync(await PlatformClientAsync(invited)));
        Assert.DoesNotContain("accept-invite-", await ReadAsync(await PlatformClientAsync(expired)));
        Assert.DoesNotContain("accept-invite-", await ReadAsync(await PlatformClientAsync(await app.MakeAccountAsync())));
    }

    [Fact]
    public async Task AcceptingMakesTheAccountAnAdmin()
    {
        var email = await app.MakeAccountAsync();
        await InviteAsync(email, DateTimeOffset.UtcNow.AddDays(1));
        var client = await PlatformClientAsync(email);

        var response = await TestApp.PostFormAsync(client, "/sites", $"accept-invite-{app.FirstSiteId.Value}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SiteRole.Admin, await RoleAsync(email));
        var page = await ReadAsync(client);
        Assert.Contains($"href=\"http://{TestApp.FirstSiteHost}/admin\"", page);
        Assert.DoesNotContain("accept-invite-", page);
    }

    [Fact]
    public async Task DecliningDeletesTheInvitation()
    {
        var email = await app.MakeAccountAsync();
        await InviteAsync(email, DateTimeOffset.UtcNow.AddDays(1));
        var client = await PlatformClientAsync(email);

        var response = await TestApp.PostFormAsync(client, "/sites", $"decline-invite-{app.FirstSiteId.Value}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await RoleAsync(email));
        Assert.DoesNotContain("accept-invite-", await ReadAsync(client));
    }

    // only admins are offered Leave: an owner hands the website over or deletes it
    [Fact]
    public async Task AnAdminCanLeaveAndTheOwnerIsntOffered()
    {
        var admin = await app.MakeFirstSiteAdminAsync();
        var formName = $"leave-site-{app.FirstSiteId.Value}";

        Assert.DoesNotContain(formName, await ReadAsync(await PlatformClientAsync(TestApp.FirstSiteOwnerEmail)));
        var response = await TestApp.PostFormAsync(await PlatformClientAsync(admin), "/sites", formName, []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await RoleAsync(admin));
    }

    private Task<HttpClient> PlatformClientAsync(string email) => app.SignedInClientAsync(TestApp.PlatformHost, email);

    private Task InviteAsync(string email, DateTimeOffset expiresAt) =>
        app.Services.GetRequiredService<SiteInviteRepository>()
            .AddAsync(app.FirstSiteId, EmailAddress.Read(email) ?? throw new InvalidOperationException("Not an email address."), expiresAt);

    private async Task<SiteRole?> RoleAsync(string email) =>
        await app.Services.GetRequiredService<SiteRepository>().GetMemberRoleAsync(app.FirstSiteId, await app.UserIdAsync(email));

    private static async Task<string> ReadAsync(HttpClient client)
    {
        var response = await client.GetAsync("/sites", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
