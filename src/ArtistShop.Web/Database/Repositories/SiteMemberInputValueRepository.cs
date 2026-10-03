namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Sites;
using Dapper;
using Npgsql;

// the platform database's record of the value each site member last left a RememberedInputs input at
public class SiteMemberInputValueRepository(NpgsqlDataSource platformDataSource)
{
    // null when the member never changed it
    public async Task<string?> GetAsync(SiteId siteId, string userId, string inputName)
    {
        await using var connection = platformDataSource.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT value FROM get_site_member_input_value(@SiteId, @UserId, @InputName)",
            new { SiteId = siteId.Value, UserId = userId, InputName = inputName }
        );
    }

    // saves nothing for a user who isn't a member of the site
    public async Task SetAsync(SiteId siteId, string userId, string inputName, string value)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT set_site_member_input_value(@SiteId, @UserId, @InputName, @Value)",
            new { SiteId = siteId.Value, UserId = userId, InputName = inputName, Value = value }
        );
    }
}
