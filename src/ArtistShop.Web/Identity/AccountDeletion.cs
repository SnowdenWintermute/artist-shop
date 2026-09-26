namespace ArtistShop.Web.Identity;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Email;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Identity;

// Deletes an account, which is the whole platform's: the websites it owns go into their grace
// period, which nobody can end now, and it stops being an admin of the rest. The platform changes
// come first, since it's a different database: if Identity's delete then fails, the account still
// exists and its owner can keep their websites. The websites' own member rows for it stay until
// they're erased
public sealed class AccountDeletion(
    UserManager<ApplicationUser> userManager,
    SiteRepository siteRepository,
    SiteMemberAccounts siteMemberAccounts,
    HostDirectory hostDirectory,
    SiteEmails siteEmails,
    AccountEmails accountEmails,
    PlatformSettings platformSettings,
    TimeProvider timeProvider
)
{
    // the websites the account owned, each with when it's erased, which are offline now
    public async Task<List<MemberSite>> DeleteAsync(ApplicationUser account)
    {
        var email =
            EmailAddress.Read(Unwrap.Value(account.Email))
            ?? throw new InvalidOperationException($"Account {account.Id}'s email isn't an email address.");
        var memberships = await siteRepository.GetForMemberAsync(account.Id);
        var eraseAt = SiteDeletion.EraseAt(timeProvider.GetUtcNow());

        // one already being deleted keeps its date, and its admins were told then
        foreach (var site in memberships.Where(site => site.Role is SiteRole.Owner && !site.IsBeingDeleted))
        {
            // read while the account still exists, as every member must have one
            var admins = (await siteMemberAccounts.GetAsync(site.SiteId)).Where(member => member.Role is SiteRole.Admin);

            if (await siteRepository.ScheduleDeletionAsync(site.SiteId, account.Id, eraseAt))
            {
                foreach (var admin in admins)
                {
                    await siteEmails.SendOwnerAccountDeletedToAdminAsync(admin.Email, email, site.MainHost, eraseAt);
                }
            }
        }

        var leftSites = memberships.Where(site => site.Role is SiteRole.Admin).ToList();

        foreach (var site in leftSites)
        {
            await siteRepository.RemoveAdminAsync(site.SiteId, account.Id);
        }

        await hostDirectory.ReloadAsync();

        // read again for each one's erase date
        var deletedSites = (await siteRepository.GetForMemberAsync(account.Id))
            .Where(site => site.Role is SiteRole.Owner)
            .ToList();

        var result = await userManager.DeleteAsync(account);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Identity didn't delete account {account.Id}.");
        }

        await accountEmails.SendAccountDeletedAsync(email.Value, platformSettings.Name, deletedSites, leftSites);

        return deletedSites;
    }
}
