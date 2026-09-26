namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Email;

// An owner deleting their site, and keeping it again within the grace period. Each change is
// followed by reloading the hosts, which is what takes the site offline or puts it back
public sealed class SiteDeletions(
    SiteRepository siteRepository,
    SiteMemberAccounts siteMemberAccounts,
    HostDirectory hostDirectory,
    SiteEmails siteEmails,
    TimeProvider timeProvider
)
{
    // Every member is emailed. False when ownerUserId isn't the owner or it's already being deleted,
    // so nothing changed. mySitesLink is where the owner can keep it
    public async Task<bool> DeleteAsync(MemberSite site, string ownerUserId, string mySitesLink)
    {
        var eraseAt = SiteDeletion.EraseAt(timeProvider.GetUtcNow());

        if (!await siteRepository.ScheduleDeletionAsync(site.SiteId, ownerUserId, eraseAt))
        {
            return false;
        }

        await hostDirectory.ReloadAsync();

        var members = await siteMemberAccounts.GetAsync(site.SiteId);
        var owner =
            members.Find(member => member.Role is SiteRole.Owner)
            ?? throw new InvalidOperationException($"Site {site.SiteId.Value} has no owner.");

        await siteEmails.SendDeletedToOwnerAsync(owner.Email, site.MainHost, eraseAt, mySitesLink);

        foreach (var admin in members.Where(member => member.Role is SiteRole.Admin))
        {
            await siteEmails.SendDeletedToAdminAsync(admin.Email, owner.Email, site.MainHost, eraseAt);
        }

        return true;
    }

    // False when ownerUserId isn't the owner or it isn't being deleted, so nothing changed
    public async Task<bool> KeepAsync(SiteId siteId, string ownerUserId)
    {
        if (!await siteRepository.KeepAsync(siteId, ownerUserId))
        {
            return false;
        }

        await hostDirectory.ReloadAsync();
        return true;
    }
}
