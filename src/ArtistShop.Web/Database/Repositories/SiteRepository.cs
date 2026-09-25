namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// The platform database's list of sites, their hosts and their members, not any one site's own data
public class SiteRepository(NpgsqlDataSource platformDataSource)
{
    private const string HostPrimaryKey = "primary_key_site_hosts";

    // the next site's id, taken before its schema is made (SiteProvisioner)
    public async Task<SiteId> ReserveIdAsync()
    {
        await using var connection = platformDataSource.CreateConnection();

        return new SiteId(await connection.ExecuteScalarAsync<int>("SELECT reserve_site_id()"));
    }

    // siteId is from ReserveIdAsync. The first host is the site's main one. ownerUserId is
    // Identity's id for the owner's account
    public async Task AddAsync(SiteId siteId, IReadOnlyList<HostName> hosts, string ownerUserId)
    {
        if (hosts.Count is 0)
        {
            throw new ArgumentException("A site needs at least one host.", nameof(hosts));
        }

        // caught here, since the database would report it as a host another site has
        if (hosts.Distinct().Count() != hosts.Count)
        {
            throw new ArgumentException("A site's hosts are listed more than once.", nameof(hosts));
        }

        await using var connection = platformDataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "SELECT add_site(@Id, @Hosts, @OwnerUserId)",
                new
                {
                    Id = siteId.Value,
                    Hosts = hosts.Select(host => host.Value).ToArray(),
                    OwnerUserId = ownerUserId,
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, HostPrimaryKey))
        {
            // the whole function is one statement's transaction, so the new site is gone too.
            // Which of the hosts clashed isn't said, so all of them are named
            throw new NameAlreadyInUseException(string.Join(", ", hosts.Select(host => host.Value)));
        }
    }

    // Sign-up's site: uses up the code and adds the site in one transaction, so the code is only
    // used if the site is made. The code is the sign-up codes' resource, but using it is what adds
    // the site, so it can't be a separate write
    public async Task AddWithSignUpCodeAsync(SignUpCode code, SiteId siteId, HostName host, string ownerUserId)
    {
        await using var connection = platformDataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "SELECT add_site_with_sign_up_code(@CodeHash, @Id, @Host, @OwnerUserId)",
                new
                {
                    CodeHash = code.Hash(),
                    Id = siteId.Value,
                    Host = host.Value,
                    OwnerUserId = ownerUserId,
                }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.SignUpCodeNotUsable))
        {
            throw new SignUpCodeNotUsableException();
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, HostPrimaryKey))
        {
            throw new NameAlreadyInUseException(host.Value);
        }
    }

    public async Task<List<SiteId>> GetIdsAsync()
    {
        await using var connection = platformDataSource.CreateConnection();

        var ids = await connection.QueryAsync<int>("SELECT * FROM get_site_ids()");

        return [.. ids.Select(id => new SiteId(id))];
    }

    public async Task<List<SiteHost>> GetHostsAsync()
    {
        await using var connection = platformDataSource.CreateConnection();

        var rows = await connection.QueryAsync<SiteHostRow>("SELECT * FROM get_site_hosts()");

        return [.. rows.Select(row => row.ToSiteHost())];
    }

    // null when the user isn't one of the site's members
    public async Task<SiteRole?> GetMemberRoleAsync(SiteId siteId, string userId)
    {
        await using var connection = platformDataSource.CreateConnection();

        // Dapper turns the smallint into the enum value with that number
        return await connection.QuerySingleOrDefaultAsync<SiteRole?>(
            "SELECT * FROM get_site_member_role(@SiteId, @UserId)",
            new { SiteId = siteId.Value, UserId = userId }
        );
    }

    private sealed class SiteHostRow
    {
        public required string Host { get; init; }
        public required int SiteId { get; init; }
        public required bool IsMain { get; init; }

        // the table's CHECK keeps every host lowercase, which is all Read changes
        public SiteHost ToSiteHost() =>
            new(
                HostName.Read(Host) ?? throw new InvalidOperationException($"site_hosts holds \"{Host}\", which isn't a host name."),
                new SiteId(SiteId),
                IsMain
            );
    }
}
