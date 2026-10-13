namespace ArtistShop.Web.Identity;

using ArtistShop.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Accounts' emails by user id, for joining the platform database's user ids to Identity's accounts
public sealed class UserEmails(UserManager<ApplicationUser> userManager)
{
    // every id must have an account
    public async Task<Dictionary<string, EmailAddress>> GetAsync(IReadOnlyCollection<string> userIds)
    {
        var emails = await GetExistingAsync(userIds);

        return userIds
            .Distinct()
            .ToDictionary(
                userId => userId,
                userId => emails.GetValueOrDefault(userId) ?? throw new InvalidOperationException($"User {userId} has no account.")
            );
    }

    // Only the ids that still have an account. For an id that can outlive its account: a deleted
    // owner's stays on their websites until AccountDeletion removes it, just after the account
    public async Task<Dictionary<string, EmailAddress>> GetExistingAsync(IReadOnlyCollection<string> userIds)
    {
        var emails = await userManager
            .Users.Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email);

        return emails.ToDictionary(
            pair => pair.Key,
            pair =>
                EmailAddress.Read(pair.Value ?? throw new InvalidOperationException($"User {pair.Key} has no email."))
                ?? throw new InvalidOperationException($"User {pair.Key}'s email isn't an email address.")
        );
    }
}
