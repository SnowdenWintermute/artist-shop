namespace ArtistShop.Web.Identity;

using ArtistShop.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Accounts' emails by user id, for joining the platform database's user ids to Identity's accounts.
// Every id must have an account with an email
public sealed class UserEmails(UserManager<ApplicationUser> userManager)
{
    public async Task<Dictionary<string, EmailAddress>> GetAsync(IReadOnlyCollection<string> userIds)
    {
        var emails = await userManager
            .Users.Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);

        return userIds
            .Distinct()
            .ToDictionary(
                userId => userId,
                userId =>
                    EmailAddress.Read(
                        emails.GetValueOrDefault(userId)
                            ?? throw new InvalidOperationException($"User {userId} has no account or no email.")
                    ) ?? throw new InvalidOperationException($"User {userId}'s email isn't an email address.")
            );
    }
}
