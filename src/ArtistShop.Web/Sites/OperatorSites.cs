namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;

// Sites with their owners' emails, for the operator. Memberships are in the platform database and
// accounts in Identity's, so the two are read separately and joined here
public sealed class OperatorSites(SiteRepository siteRepository, UserEmails userEmails)
{
    // in no particular order
    public async Task<List<OperatorSite>> GetAllAsync() => await WithOwnerEmailsAsync(await siteRepository.GetAllAsync());

    // null when there's no such site, as when it was erased since the page loaded
    public async Task<OperatorSite?> GetAsync(SiteId siteId) =>
        await siteRepository.GetAsync(siteId) is { } site ? (await WithOwnerEmailsAsync([site]))[0] : null;

    private async Task<List<OperatorSite>> WithOwnerEmailsAsync(List<SiteListing> sites)
    {
        var emails = await userEmails.GetAsync([.. sites.Select(site => site.OwnerUserId).OfType<string>()]);

        return [.. sites.Select(site => new OperatorSite(site, site.OwnerUserId is { } userId ? emails[userId] : null))];
    }
}
