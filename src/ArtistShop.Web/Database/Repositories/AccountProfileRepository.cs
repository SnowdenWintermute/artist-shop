namespace ArtistShop.Web.Database.Repositories;

using Dapper;
using Npgsql;

// the platform database's row for each account, which its other account rows hang off
public class AccountProfileRepository(NpgsqlDataSource platformDataSource)
{
    // nothing when it has one already
    public async Task AddAsync(string userId)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync("SELECT add_account_profile(@UserId)", new { UserId = userId });
    }

    // with everything hung off it, such as its dismissed hints
    public async Task DeleteAsync(string userId)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync("SELECT delete_account_profile(@UserId)", new { UserId = userId });
    }
}
