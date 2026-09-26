using System.Net;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// A site's Admins page, where its owner invites and removes admins
[Collection(TestAppCollection.Name)]
public sealed class SiteAdminsTests(TestApp app)
{
    private const string Path = "/admin/admins";

    [Fact]
    public async Task TheOwnerSeesTheMembers()
    {
        var admin = await app.MakeFirstSiteAdminAsync();

        var page = await ReadAsync(await OwnerClientAsync());

        Assert.Contains(TestApp.FirstSiteOwnerEmail, page);
        Assert.Contains(admin, page);
        // where an invited admin accepts, spelled out, since they may not know the platform's address
        Assert.Contains($">http://{TestApp.PlatformHost}/sites</a>", page);
    }

    [Fact]
    public async Task AnAdminIsDenied()
    {
        var client = await app.SignedInClientAsync(TestApp.FirstSiteHost, await app.MakeFirstSiteAdminAsync());

        var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task SomeoneNotSignedInIsSentToSignIn()
    {
        var response = await app.ClientFor(TestApp.FirstSiteHost).GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // the address is kept as invitations keep it, and the email names the site and links to My websites
    // on the platform
    [Fact]
    public async Task InvitingEmailsTheAddressAndListsTheInvitation()
    {
        var email = $"{Guid.NewGuid():n}@example.com";
        var client = await OwnerClientAsync();

        var response = await InviteAsync(client, $"  {email.ToUpperInvariant()} ");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(app.Mailer.SentTo(email));
        Assert.Contains($"href=\"http://{TestApp.PlatformHost}/sites\">http://{TestApp.PlatformHost}/sites</a>", sent.HtmlBody);
        Assert.Contains(TestApp.FirstSiteOwnerEmail, sent.HtmlBody);
        Assert.Contains(TestApp.FirstSiteHost, sent.Subject);
        Assert.Contains(email, await ReadAsync(client));
    }

    [Fact]
    public async Task AMemberIsntInvited()
    {
        var admin = await app.MakeFirstSiteAdminAsync();

        var response = await InviteAsync(await OwnerClientAsync(), admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This account already helps run this website.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(app.Mailer.SentTo(admin));
    }

    // each row's dialog posts a form named for that row
    [Fact]
    public async Task RemovingAnAdminTakesTheirMembership()
    {
        var admin = await app.MakeFirstSiteAdminAsync();
        var adminUserId = await app.UserIdAsync(admin);

        var response = await TestApp.PostFormAsync(await OwnerClientAsync(), Path, $"remove-admin-{adminUserId}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await app.Services.GetRequiredService<SiteRepository>().GetMemberRoleAsync(app.FirstSiteId, adminUserId));
    }

    [Fact]
    public async Task RevokingAnInvitationDeletesIt()
    {
        var email = EmailAddress.Read($"{Guid.NewGuid():n}@example.com") ?? throw new InvalidOperationException("Not an email address.");
        var invites = app.Services.GetRequiredService<SiteInviteRepository>();
        await invites.AddAsync(app.FirstSiteId, email, DateTimeOffset.UtcNow.AddDays(1));

        var response = await TestApp.PostFormAsync(await OwnerClientAsync(), Path, $"revoke-invite-{email.Value}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain(await invites.GetForSiteAsync(app.FirstSiteId), invite => invite.Email == email);
    }

    // Both are emailed, and the old owner, now an admin, goes to the dashboard since this page is
    // the owner's only. On a site of its own, since the other tests need the first site's owner
    [Fact]
    public async Task MakingAnAdminTheOwnerSwapsTheirRoles()
    {
        var site = await app.MakeSiteAsync();
        var admin = await app.MakeAdminAsync(site.Id);
        var adminUserId = await app.UserIdAsync(admin);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(client, Path, $"hand-over-{adminUserId}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.AbsolutePath);
        var sites = app.Services.GetRequiredService<SiteRepository>();
        Assert.Equal(SiteRole.Owner, await sites.GetMemberRoleAsync(site.Id, adminUserId));
        Assert.Equal(SiteRole.Admin, await sites.GetMemberRoleAsync(site.Id, await app.UserIdAsync(site.OwnerEmail)));
        Assert.Contains(site.OwnerEmail, Assert.Single(app.Mailer.SentTo(admin)).HtmlBody);
        Assert.Contains(admin, Assert.Single(app.Mailer.SentTo(site.OwnerEmail)).HtmlBody);

        var adminsPage = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal("/Account/AccessDenied", adminsPage.Headers.Location?.AbsolutePath);
    }

    private Task<HttpClient> OwnerClientAsync() => app.SignedInClientAsync(TestApp.FirstSiteHost, TestApp.FirstSiteOwnerEmail);

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, string email) =>
        TestApp.PostFormAsync(client, Path, "invite-admin", new() { ["Input.Email"] = email });

    private static async Task<string> ReadAsync(HttpClient client)
    {
        var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
