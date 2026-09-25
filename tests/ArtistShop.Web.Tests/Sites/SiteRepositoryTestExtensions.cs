using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Tests.Sites;

// A site's row with a freshly reserved id, as SiteProvisioner adds it, but with no schema: the tests
// of the platform's tables need only the row
public static class SiteRepositoryTestExtensions
{
    public static async Task<SiteId> AddNewAsync(this SiteRepository sites, IReadOnlyList<HostName> hosts, string ownerUserId)
    {
        var siteId = await sites.ReserveIdAsync();
        await sites.AddAsync(siteId, hosts, ownerUserId);
        return siteId;
    }

    public static async Task<SiteId> AddNewWithSignUpCodeAsync(
        this SiteRepository sites,
        SignUpCode code,
        HostName host,
        string ownerUserId
    )
    {
        var siteId = await sites.ReserveIdAsync();
        await sites.AddWithSignUpCodeAsync(code, siteId, host, ownerUserId);
        return siteId;
    }
}
