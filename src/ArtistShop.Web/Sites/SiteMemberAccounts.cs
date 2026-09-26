namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// A site's members with their emails. Memberships are in the platform database and accounts in
// Identity's, so the two are read separately and joined here
public sealed class SiteMemberAccounts(SiteRepository siteRepository, UserManager<ApplicationUser> userManager)
{
    // in no particular order
    public async Task<List<SiteMemberAccount>> GetAsync(SiteId siteId)
    {
        var members = await siteRepository.GetMembersAsync(siteId);
        var userIds = members.Select(member => member.UserId).ToList();
        var emails = await userManager
            .Users.Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);

        return
        [
            .. members.Select(member => new SiteMemberAccount(
                member.UserId,
                EmailAddress.Read(
                    emails.GetValueOrDefault(member.UserId)
                        ?? throw new InvalidOperationException($"Member {member.UserId} has no account or no email.")
                ) ?? throw new InvalidOperationException($"Member {member.UserId}'s email isn't an email address."),
                member.Role
            )),
        ];
    }
}
