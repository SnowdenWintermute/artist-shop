using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteRepositoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);
    private readonly SignUpCodeRepository _codes = new(database.PlatformDataSource);

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    // unique to one test, since every test shares the platform test database
    private static HostName NewHost(string label) =>
        HostName.Read($"{label}-{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");

    [Fact]
    public async Task KeepsASitesHostsWithTheFirstAsMain()
    {
        var main = NewHost("main");
        var other = NewHost("other");

        var siteId = await _sites.AddNewAsync([main, other], OwnerUserId);

        var hosts = (await _sites.GetHostsAsync()).Where(host => host.SiteId == siteId).OrderBy(host => host.Host.Value);
        Assert.Equivalent(new[] { new SiteHost(main, siteId, true), new SiteHost(other, siteId, false) }, hosts);
        Assert.Contains(siteId, await _sites.GetIdsAsync());
    }

    [Fact]
    public async Task RefusesAHostAnotherSiteHasAndAddsNoSite()
    {
        var taken = NewHost("taken");
        await _sites.AddNewAsync([taken], OwnerUserId);
        var sitesBefore = await _sites.GetIdsAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _sites.AddNewAsync([NewHost("new"), taken], OwnerUserId));

        Assert.Equal(sitesBefore.Count, (await _sites.GetIdsAsync()).Count);
    }

    [Fact]
    public async Task RefusesAHostListedTwiceAndAddsNoSite()
    {
        var host = NewHost("twice");
        var sitesBefore = await _sites.GetIdsAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _sites.AddNewAsync([host, host], OwnerUserId));

        Assert.Equal(sitesBefore.Count, (await _sites.GetIdsAsync()).Count);
    }

    [Fact]
    public async Task TheSitesOwnerIsItsOnlyMember()
    {
        var siteId = await _sites.AddNewAsync([NewHost("owned")], OwnerUserId);

        Assert.Equal(SiteRole.Owner, await _sites.GetMemberRoleAsync(siteId, OwnerUserId));
        Assert.Null(await _sites.GetMemberRoleAsync(siteId, "someone-else"));
    }

    // membership is of one site, not every site the account is on
    [Fact]
    public async Task OwningOneSiteIsNoRoleOnAnother()
    {
        await _sites.AddNewAsync([NewHost("mine")], "first-owner");
        var other = await _sites.AddNewAsync([NewHost("theirs")], "second-owner");

        Assert.Null(await _sites.GetMemberRoleAsync(other, "first-owner"));
    }

    [Fact]
    public async Task ASignUpCodeAddsASiteOwnedByTheAccountAndIsUsedUp()
    {
        var code = await NewSignUpCodeAsync(DateTimeOffset.UtcNow.AddDays(1));
        var host = NewHost("signed-up");

        var siteId = await _sites.AddNewWithSignUpCodeAsync(code, host, OwnerUserId);

        Assert.Contains(new SiteHost(host, siteId, IsMain: true), await _sites.GetHostsAsync());
        Assert.Equal(SiteRole.Owner, await _sites.GetMemberRoleAsync(siteId, OwnerUserId));
    }

    [Fact]
    public async Task AUsedSignUpCodeAddsNoSecondSite()
    {
        var code = await NewSignUpCodeAsync(DateTimeOffset.UtcNow.AddDays(1));
        await _sites.AddNewWithSignUpCodeAsync(code, NewHost("first"), OwnerUserId);
        var secondHost = NewHost("second");

        await Assert.ThrowsAsync<SignUpCodeNotUsableException>(() =>
            _sites.AddNewWithSignUpCodeAsync(code, secondHost, OwnerUserId)
        );
        Assert.DoesNotContain(await _sites.GetHostsAsync(), siteHost => siteHost.Host == secondHost);
    }

    [Fact]
    public async Task AnExpiredSignUpCodeAddsNoSite()
    {
        var code = await NewSignUpCodeAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        var host = NewHost("expired");

        await Assert.ThrowsAsync<SignUpCodeNotUsableException>(() =>
            _sites.AddNewWithSignUpCodeAsync(code, host, OwnerUserId)
        );
        Assert.DoesNotContain(await _sites.GetHostsAsync(), siteHost => siteHost.Host == host);
    }

    [Fact]
    public async Task ACodeNeverMadeAddsNoSite() =>
        await Assert.ThrowsAsync<SignUpCodeNotUsableException>(() =>
            _sites.AddNewWithSignUpCodeAsync(SignUpCode.New(), NewHost("never-made"), OwnerUserId)
        );

    // the code is used only if the site is made, so it can be tried again with another name
    [Fact]
    public async Task ATakenHostLeavesTheSignUpCodeUnused()
    {
        var host = NewHost("taken");
        await _sites.AddNewAsync([host], OwnerUserId);
        var code = await NewSignUpCodeAsync(DateTimeOffset.UtcNow.AddDays(1));

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _sites.AddNewWithSignUpCodeAsync(code, host, OwnerUserId)
        );
        await _sites.AddNewWithSignUpCodeAsync(code, NewHost("another"), OwnerUserId);
    }

    // the sites the account is a member of, each by its main host, and no one else's
    [Fact]
    public async Task AMembersSitesAreListedByTheirMainHosts()
    {
        var owner = $"owner-{Guid.NewGuid():n}";
        var main = NewHost("listed");
        var siteId = await _sites.AddNewAsync([main, NewHost("other-host")], owner);
        await _sites.AddNewAsync([NewHost("someone-elses")], "someone-else");

        var site = Assert.Single(await _sites.GetForMemberAsync(owner));

        Assert.Equal(new MemberSite(siteId, main, SiteRole.Owner), site);
    }

    [Fact]
    public async Task ASitesMembersAreListedWithTheirRoles()
    {
        var siteId = await _sites.AddNewAsync([NewHost("members")], OwnerUserId);
        await AddAdminAsync(siteId, "admin");

        var members = await _sites.GetMembersAsync(siteId);

        Assert.Equivalent(new[] { new SiteMember(OwnerUserId, SiteRole.Owner), new SiteMember("admin", SiteRole.Admin) }, members);
    }

    [Fact]
    public async Task RemovingAnAdminNeverRemovesTheOwner()
    {
        var siteId = await _sites.AddNewAsync([NewHost("removing")], OwnerUserId);
        await AddAdminAsync(siteId, "admin");

        await _sites.RemoveAdminAsync(siteId, "admin");
        await _sites.RemoveAdminAsync(siteId, OwnerUserId);

        Assert.Equal(new SiteMember(OwnerUserId, SiteRole.Owner), Assert.Single(await _sites.GetMembersAsync(siteId)));
    }

    [Fact]
    public async Task HandingOverSwapsTheOwnerAndTheAdmin()
    {
        var siteId = await _sites.AddNewAsync([NewHost("handing-over")], OwnerUserId);
        await AddAdminAsync(siteId, "admin");

        Assert.True(await _sites.HandOverAsync(siteId, OwnerUserId, "admin"));

        Assert.Equivalent(
            new[] { new SiteMember(OwnerUserId, SiteRole.Admin), new SiteMember("admin", SiteRole.Owner) },
            await _sites.GetMembersAsync(siteId)
        );
    }

    // such as an admin who left, or a second hand-over by an owner who already handed the site over
    [Theory]
    [InlineData(OwnerUserId, "not-a-member")]
    [InlineData("admin", OwnerUserId)]
    [InlineData("other-admin", "admin")]
    public async Task HandingOverFromOtherThanTheOwnerOrToOtherThanAnAdminChangesNothing(string fromUserId, string toUserId)
    {
        var siteId = await _sites.AddNewAsync([NewHost("not-handing-over")], OwnerUserId);
        await AddAdminAsync(siteId, "admin");
        await AddAdminAsync(siteId, "other-admin");
        var before = await _sites.GetMembersAsync(siteId);

        Assert.False(await _sites.HandOverAsync(siteId, fromUserId, toUserId));

        Assert.Equivalent(before, await _sites.GetMembersAsync(siteId));
    }

    // as an accepted invitation makes one
    private async Task AddAdminAsync(SiteId siteId, string userId)
    {
        var invites = new SiteInviteRepository(database.PlatformDataSource);
        var email = EmailAddress.Read($"{Guid.NewGuid():n}@example.com") ?? throw new InvalidOperationException("Not an email address.");
        await invites.AddAsync(siteId, email, DateTimeOffset.UtcNow.AddDays(1));
        await invites.AcceptAsync(siteId, email, userId);
    }

    private async Task<SignUpCode> NewSignUpCodeAsync(DateTimeOffset expiresAt)
    {
        var code = SignUpCode.New();
        await _codes.AddAsync(code, "a test", expiresAt);
        return code;
    }
}
