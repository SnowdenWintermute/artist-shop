namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// the platform database's invitations for an email to become one of a site's admins
public class SiteInviteRepository(NpgsqlDataSource platformDataSource)
{
    // replaces any invitation the email already has to the site
    public async Task AddAsync(SiteId siteId, EmailAddress email, DateTimeOffset expiresAt)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT add_site_invite(@SiteId, @Email, @ExpiresAt)",
            new { SiteId = siteId.Value, Email = email.Value, ExpiresAt = expiresAt }
        );
    }

    public async Task DeleteAsync(SiteId siteId, EmailAddress email)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT delete_site_invite(@SiteId, @Email)",
            new { SiteId = siteId.Value, Email = email.Value }
        );
    }

    // expired or not, in no particular order
    public async Task<List<SiteInvite>> GetForSiteAsync(SiteId siteId)
    {
        await using var connection = platformDataSource.CreateConnection();

        var rows = await connection.QueryAsync<SiteInviteRow>(
            "SELECT * FROM get_site_invites(@SiteId)",
            new { SiteId = siteId.Value }
        );

        return [.. rows.Select(row => row.ToSiteInvite())];
    }

    // unexpired only, in no particular order
    public async Task<List<ReceivedInvite>> GetForEmailAsync(EmailAddress email)
    {
        await using var connection = platformDataSource.CreateConnection();

        var rows = await connection.QueryAsync<ReceivedInviteRow>(
            "SELECT * FROM get_email_invites(@Email)",
            new { Email = email.Value }
        );

        return [.. rows.Select(row => row.ToReceivedInvite())];
    }

    // Uses up the invitation and makes the account one of the site's admins. Adding the member is
    // the members' resource, but using the invitation is what adds them, so it can't be a separate
    // write. False when the invitation had expired or was gone, so nothing changed
    public async Task<bool> AcceptAsync(SiteId siteId, EmailAddress email, string userId)
    {
        await using var connection = platformDataSource.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            "SELECT accept_site_invite(@SiteId, @Email, @UserId)",
            new { SiteId = siteId.Value, Email = email.Value, UserId = userId }
        );
    }

    // Npgsql reads a timestamptz as a DateTime in UTC, which is what these hold
    private sealed class SiteInviteRow
    {
        public required string Email { get; init; }
        public required DateTime InvitedAt { get; init; }
        public required DateTime ExpiresAt { get; init; }

        public SiteInvite ToSiteInvite() =>
            new(
                EmailAddress.Read(Email)
                    ?? throw new InvalidOperationException($"site_invites holds \"{Email}\", which isn't an email address."),
                new DateTimeOffset(InvitedAt),
                new DateTimeOffset(ExpiresAt)
            );
    }

    private sealed class ReceivedInviteRow
    {
        public required int SiteId { get; init; }
        public required string MainHost { get; init; }
        public required DateTime ExpiresAt { get; init; }

        public ReceivedInvite ToReceivedInvite() =>
            new(new SiteId(SiteId), SiteRepository.ReadStoredHost(MainHost), new DateTimeOffset(ExpiresAt));
    }
}
