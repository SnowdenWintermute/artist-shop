using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Tests.Database;
using ArtistShop.Web.Tests.Sites;

namespace ArtistShop.Web.Tests.Platform;

[Collection(DatabaseCollection.Name)]
public sealed class SiteInviteRepositoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);
    private readonly SiteInviteRepository _invites = new(database.PlatformDataSource);

    private static readonly DateTimeOffset Tomorrow = DateTimeOffset.UtcNow.AddDays(1);

    [Fact]
    public async Task AcceptingMakesAnAdminAndUsesUpTheInvitation()
    {
        var (siteId, main) = await NewSiteAsync();
        var email = NewEmail();
        await _invites.AddAsync(siteId, email, Tomorrow);
        Assert.Equal(main, Assert.Single(await _invites.GetForEmailAsync(email)).MainHost);

        Assert.True(await _invites.AcceptAsync(siteId, email, "new-admin"));

        Assert.Equal(SiteRole.Admin, await _sites.GetMemberRoleAsync(siteId, "new-admin"));
        Assert.Empty(await _invites.GetForEmailAsync(email));
        Assert.Empty(await _invites.GetForSiteAsync(siteId));
        Assert.False(await _invites.AcceptAsync(siteId, email, "someone-else"));
    }

    // still listed for the owner, marked expired, but not for the invited email
    [Fact]
    public async Task AnExpiredInvitationIsntAccepted()
    {
        var (siteId, _) = await NewSiteAsync();
        var email = NewEmail();
        await _invites.AddAsync(siteId, email, DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(await _invites.AcceptAsync(siteId, email, "late"));

        Assert.Null(await _sites.GetMemberRoleAsync(siteId, "late"));
        Assert.Empty(await _invites.GetForEmailAsync(email));
        Assert.Single(await _invites.GetForSiteAsync(siteId));
    }

    [Fact]
    public async Task AnotherEmailsInvitationIsntAccepted()
    {
        var (siteId, _) = await NewSiteAsync();
        await _invites.AddAsync(siteId, NewEmail(), Tomorrow);

        Assert.False(await _invites.AcceptAsync(siteId, NewEmail(), "stranger"));

        Assert.Null(await _sites.GetMemberRoleAsync(siteId, "stranger"));
    }

    [Fact]
    public async Task InvitingAgainReplacesTheInvitation()
    {
        var (siteId, _) = await NewSiteAsync();
        var email = NewEmail();
        var later = new DateTimeOffset(2031, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await _invites.AddAsync(siteId, email, Tomorrow);

        await _invites.AddAsync(siteId, email, later);

        Assert.Equal(later, Assert.Single(await _invites.GetForSiteAsync(siteId)).ExpiresAt);
    }

    // the owner accepting an invitation to their own site stays its owner
    [Fact]
    public async Task AMemberAcceptingKeepsTheirRole()
    {
        var (siteId, _) = await NewSiteAsync();
        var email = NewEmail();
        await _invites.AddAsync(siteId, email, Tomorrow);

        Assert.True(await _invites.AcceptAsync(siteId, email, "owner"));

        Assert.Equal(SiteRole.Owner, await _sites.GetMemberRoleAsync(siteId, "owner"));
    }

    [Fact]
    public async Task DeletingRemovesOnlyThatInvitation()
    {
        var (siteId, _) = await NewSiteAsync();
        var kept = NewEmail();
        var deleted = NewEmail();
        await _invites.AddAsync(siteId, kept, Tomorrow);
        await _invites.AddAsync(siteId, deleted, Tomorrow);

        await _invites.DeleteAsync(siteId, deleted);

        Assert.Equal(kept, Assert.Single(await _invites.GetForSiteAsync(siteId)).Email);
    }

    private async Task<(SiteId SiteId, HostName Main)> NewSiteAsync()
    {
        var main = HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
        return (await _sites.AddNewAsync([main], "owner"), main);
    }

    // unique to one test, since every test shares the platform test database
    private static EmailAddress NewEmail() =>
        EmailAddress.Read($"{Guid.NewGuid():n}@example.com") ?? throw new InvalidOperationException("Not an email address.");
}
