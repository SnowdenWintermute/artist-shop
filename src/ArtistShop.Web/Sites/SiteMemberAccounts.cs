namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;

// A site's members with their emails. Memberships are in the platform database and accounts in
// Identity's, so the two are read separately and joined here
public sealed class SiteMemberAccounts(SiteRepository siteRepository, UserEmails userEmails)
{
    // in no particular order
    public async Task<List<SiteMemberAccount>> GetAsync(SiteId siteId)
    {
        var members = await siteRepository.GetMembersAsync(siteId);
        var emails = await userEmails.GetAsync([.. members.Select(member => member.UserId)]);

        return [.. members.Select(member => new SiteMemberAccount(member.UserId, emails[member.UserId], member.Role))];
    }
}
