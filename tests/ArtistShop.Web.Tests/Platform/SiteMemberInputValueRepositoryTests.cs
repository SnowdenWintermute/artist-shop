using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Tests.Database;
using ArtistShop.Web.Tests.Sites;

namespace ArtistShop.Web.Tests.Platform;

[Collection(DatabaseCollection.Name)]
public sealed class SiteMemberInputValueRepositoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);
    private readonly SiteInviteRepository _invites = new(database.PlatformDataSource);
    private readonly SiteMemberInputValueRepository _inputValues = new(database.PlatformDataSource);

    private const string InputName = RememberedInputs.WorkType;

    [Fact]
    public async Task ReadsBackTheLastValueSaved()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");
        Assert.Null(await _inputValues.GetAsync(siteId, "owner", InputName));

        await _inputValues.SetAsync(siteId, "owner", InputName, "1");
        await _inputValues.SetAsync(siteId, "owner", InputName, "2");

        Assert.Equal("2", await _inputValues.GetAsync(siteId, "owner", InputName));
    }

    [Fact]
    public async Task SavesNothingForSomeoneWhoIsntAMember()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");

        await _inputValues.SetAsync(siteId, "stranger", InputName, "1");

        Assert.Null(await _inputValues.GetAsync(siteId, "stranger", InputName));
    }

    // the values hang off the membership, so they go with it
    [Fact]
    public async Task RemovingAnAdminDeletesTheirValues()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");
        var email = EmailAddress.Read($"{Guid.NewGuid():n}@example.com") ?? throw new InvalidOperationException("Not an email address.");
        await _invites.AddAsync(siteId, email, DateTimeOffset.UtcNow.AddDays(1));
        await _invites.AcceptAsync(siteId, email, "admin");
        await _inputValues.SetAsync(siteId, "admin", InputName, "1");
        await _inputValues.SetAsync(siteId, "owner", InputName, "2");

        await _sites.RemoveAdminAsync(siteId, "admin");
        await _invites.AddAsync(siteId, email, DateTimeOffset.UtcNow.AddDays(1));
        await _invites.AcceptAsync(siteId, email, "admin");

        Assert.Null(await _inputValues.GetAsync(siteId, "admin", InputName));
        Assert.Equal("2", await _inputValues.GetAsync(siteId, "owner", InputName));
    }

    // unique to one test, since every test shares the platform test database
    private static HostName NewHost() =>
        HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
}
