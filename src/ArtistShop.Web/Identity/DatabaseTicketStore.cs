namespace ArtistShop.Web.Identity;

using System.Security.Claims;
using System.Security.Cryptography;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Sign-ins kept as rows (UserSession), so the sign-in cookie holds only a random key and a sign-in
// ends the moment its row can't be read. Signing out deletes the row, deleting the account cascades
// to its rows, and a row whose security stamp no longer matches the account's is refused. Identity
// gives the account a new stamp on a password change or reset, an email change and a removed login
// (and UserManager.UpdateSecurityStampAsync does it on purpose), so each of those signs out every
// other browser at once; the page that made the change signs its own browser in again. A website's
// sign-in hangs off the platform sign-in it was handed over from (ParentId), so logging out anywhere
// (EndBrowserAsync) ends all of a browser's sign-ins at once.
// A singleton, as the cookie options hold it, so each call opens its own scope for the database
public sealed class DatabaseTicketStore(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider
) : ITicketStore
{
    // the row's id, added to the signed-in user's claims so the interactive revalidation can find it
    public const string SessionIdClaimType = "ArtistShop.SessionId";

    // the platform sign-in a website's comes from, passed in the sign-in's AuthenticationProperties
    // by the website's handoff
    public const string ParentIdItem = "ArtistShop.ParentSessionId";

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var claims = identityOptions.Value.ClaimsIdentity;
        var id = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        ticket.Principal.Identities.First().AddClaim(new Claim(SessionIdClaimType, id));

        await using var scope = scopeFactory.CreateAsyncScope();
        var database = Database(scope);

        database.UserSessions.Add(
            new UserSession
            {
                Id = id,
                UserId = Unwrap.Value(ticket.Principal.FindFirstValue(claims.UserIdClaimType)),
                SecurityStamp = Unwrap.Value(ticket.Principal.FindFirstValue(claims.SecurityStampClaimType)),
                Ticket = TicketSerializer.Default.Serialize(ticket),
                ExpiresAt = Unwrap.Value(ticket.Properties.ExpiresUtc),
                ParentId = ticket.Properties.Items.TryGetValue(ParentIdItem, out var parentId) ? parentId : null,
            }
        );
        await database.SaveChangesAsync();

        return id;
    }

    // The cookie's sliding expiry, the stamp check's fresh claims, and a sign-in over this one: the
    // cookie handler renews the row rather than making another, as when a page signs its browser in
    // again after a password change, so the stamp comes from the ticket too. A ticket only ever
    // carries a stamp the account had when the server built it, and only an existing row is updated,
    // so one ended since this request read it stays ended. Only the row's own account's ticket is
    // written: ApplicationSignInManager signs out before another account signs in, and were that
    // missed, the row stays that account's and ends rather than becoming this one's
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var claims = identityOptions.Value.ClaimsIdentity;

        // the stamp check rebuilds the claims from the account, without this one
        if (!ticket.Principal.HasClaim(claim => claim.Type == SessionIdClaimType))
        {
            ticket.Principal.Identities.First().AddClaim(new Claim(SessionIdClaimType, key));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var ticketBytes = TicketSerializer.Default.Serialize(ticket);
        var expiresAt = Unwrap.Value(ticket.Properties.ExpiresUtc);
        var userId = Unwrap.Value(ticket.Principal.FindFirstValue(claims.UserIdClaimType));
        var securityStamp = Unwrap.Value(ticket.Principal.FindFirstValue(claims.SecurityStampClaimType));

        var database = Database(scope);

        await database
            .UserSessions.Where(session => session.Id == key && session.UserId == userId)
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(session => session.Ticket, ticketBytes)
                    .SetProperty(session => session.ExpiresAt, expiresAt)
                    .SetProperty(session => session.SecurityStamp, securityStamp)
            );

        // A website in use keeps its platform sign-in, whose row it lives by, from expiring first.
        // RetrieveAsync gives the platform's ticket this later expiry, or its cookie handler would
        // still end it, and every website's with it, at the ticket's own
        await database
            .UserSessions.Where(parent =>
                database.UserSessions.Any(session => session.Id == key && session.ParentId == parent.Id)
                && parent.ExpiresAt < expiresAt
            )
            .ExecuteUpdateAsync(setters => setters.SetProperty(parent => parent.ExpiresAt, expiresAt));
    }

    // The row's expiry is the one that counts: RenewAsync extends a platform sign-in's row without
    // its ticket
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = await Live(Database(scope), key)
            .Select(session => new { session.Ticket, session.ExpiresAt })
            .SingleOrDefaultAsync();

        if (session is null || TicketSerializer.Default.Deserialize(session.Ticket) is not { } ticket)
        {
            return null;
        }

        ticket.Properties.ExpiresUtc = session.ExpiresAt;
        return ticket;
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await Database(scope).UserSessions.Where(session => session.Id == key).ExecuteDeleteAsync();
    }

    // Logging out, on any host: deletes the browser's platform sign-in, which every website sign-in
    // it handed over is deleted with, or this sign-in itself if it has no parent. Not RemoveAsync,
    // which the cookie handler also calls for one expired sign-in
    public async Task EndBrowserAsync(ClaimsPrincipal user)
    {
        if (user.FindFirstValue(SessionIdClaimType) is not { } id)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var database = Database(scope);

        await database
            .UserSessions.Where(session =>
                session.Id == id || database.UserSessions.Any(child => child.Id == id && child.ParentId == session.Id)
            )
            .ExecuteDeleteAsync();
    }

    // for the interactive revalidation, which has the claims but no cookie
    public async Task<bool> IsLiveAsync(ClaimsPrincipal user) =>
        user.FindFirstValue(SessionIdClaimType) is { } id && await IsLiveAsync(id);

    // for a website's handoff, which a platform sign-in ended since its code was made mustn't finish
    public async Task<bool> IsLiveAsync(string id)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await Live(Database(scope), id).AnyAsync();
    }

    // rows that could never be read again, by the daily cleanup
    public async Task DeleteEndedAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = Database(scope);
        var now = timeProvider.GetUtcNow();

        await database
            .UserSessions.Where(session =>
                session.ExpiresAt <= now
                || !database.Users.Any(user => user.Id == session.UserId && user.SecurityStamp == session.SecurityStamp)
            )
            .ExecuteDeleteAsync();
    }

    // the row, unless it has expired or the account's stamp has changed since it signed in
    private IQueryable<UserSession> Live(ApplicationDbContext database, string id)
    {
        var now = timeProvider.GetUtcNow();

        return database.UserSessions.Where(session =>
            session.Id == id
            && session.ExpiresAt > now
            && database.Users.Any(user => user.Id == session.UserId && user.SecurityStamp == session.SecurityStamp)
        );
    }

    private static ApplicationDbContext Database(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
}
