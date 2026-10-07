namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Email;

// An owner deleting their site, and keeping it again within the grace period, and the operator
// deleting one outright. Each change is followed by reloading the hosts, which is what takes the site
// offline or puts it back
public sealed class SiteDeletions(
    SiteRepository siteRepository,
    SiteMemberAccounts siteMemberAccounts,
    HostDirectory hostDirectory,
    SiteEmails siteEmails,
    SiteEraser siteEraser,
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

    // The operator's deletion, with no grace period: offline at once, then erased. ownerMessage, if
    // given, is emailed to the owner, who must then have one. False when there's no such site, as when
    // it was erased since the page loaded. Erasing that fails partway is logged and left to the daily
    // cleanup, as the site is offline and due by then
    public async Task<bool> EraseNowAsync(OperatorSite site, string? ownerMessage)
    {
        if (!await siteRepository.ScheduleErasingNowAsync(site.Site.SiteId, timeProvider.GetUtcNow()))
        {
            return false;
        }

        await hostDirectory.ReloadAsync();

        if (ownerMessage is not null)
        {
            var ownerEmail =
                site.OwnerEmail ?? throw new InvalidOperationException($"Site {site.Site.SiteId.Value} has no owner to email.");
            await siteEmails.SendErasedByOperatorToOwnerAsync(ownerEmail, site.Site.MainHost, ownerMessage);
        }

        await siteEraser.EraseAsync(site.Site.SiteId);
        return true;
    }

    // False when ownerUserId isn't the owner, it isn't being deleted, or its erase date has come, so
    // nothing changed
    public async Task<bool> KeepAsync(SiteId siteId, string ownerUserId)
    {
        if (!await siteRepository.KeepAsync(siteId, ownerUserId, timeProvider.GetUtcNow()))
        {
            return false;
        }

        await hostDirectory.ReloadAsync();
        return true;
    }
}
