namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Every site with its owner's email, for the operator. Memberships are in the platform database and
// accounts in Identity's, so the two are read separately and joined here, as SiteMemberAccounts does
// for one site's members
public sealed class OperatorSites(SiteRepository siteRepository, UserManager<ApplicationUser> userManager)
{
    // in no particular order
    public async Task<List<OperatorSite>> GetAllAsync()
    {
        var sites = await siteRepository.GetAllAsync();
        var ownerUserIds = sites.Select(site => site.OwnerUserId).OfType<string>().ToList();
        var emails = await userManager
            .Users.Where(user => ownerUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);

        return [.. sites.Select(site => new OperatorSite(site, site.OwnerUserId is { } userId ? EmailOf(userId) : null))];

        EmailAddress EmailOf(string userId) =>
            EmailAddress.Read(
                emails.GetValueOrDefault(userId) ?? throw new InvalidOperationException($"Owner {userId} has no account or no email.")
            ) ?? throw new InvalidOperationException($"Owner {userId}'s email isn't an email address.");
    }

    // null when there's no such site, as when it was erased since the page loaded
    public async Task<OperatorSite?> GetAsync(SiteId siteId) =>
        (await GetAllAsync()).Find(site => site.Site.SiteId == siteId);
}
