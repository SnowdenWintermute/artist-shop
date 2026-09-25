namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// The platform database's list of sites and their hosts, not any one site's own data
public class SiteRepository(NpgsqlDataSource platformDataSource)
{
    private const string HostPrimaryKey = "primary_key_site_hosts";

    // the first host is the site's main one
    public async Task<SiteId> AddAsync(IReadOnlyList<HostName> hosts)
    {
        if (hosts.Count is 0)
        {
            throw new ArgumentException("A site needs at least one host.", nameof(hosts));
        }

        await using var connection = platformDataSource.CreateConnection();

        try
        {
            var id = await connection.ExecuteScalarAsync<int>(
                "SELECT add_site(@Hosts)",
                new { Hosts = hosts.Select(host => host.Value).ToArray() }
            );

            return new SiteId(id);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, HostPrimaryKey))
        {
            // the whole function is one statement's transaction, so the new site is gone too.
            // Which of the hosts clashed isn't said, so all of them are named
            throw new NameAlreadyInUseException(string.Join(", ", hosts.Select(host => host.Value)));
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
