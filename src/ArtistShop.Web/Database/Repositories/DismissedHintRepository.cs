namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Components.Hints;
using Dapper;
using Npgsql;

// the platform database's record of the hints each account dismissed, stored by HintType name
public class DismissedHintRepository(NpgsqlDataSource platformDataSource)
{
    public async Task<IReadOnlySet<HintType>> GetAsync(string userId)
    {
        await using var connection = platformDataSource.CreateConnection();

        var names = (
            await connection.QueryAsync<string>(
                "SELECT hint FROM get_account_dismissed_hints(@UserId)",
                new { UserId = userId }
            )
        ).ToHashSet();

        return Enum.GetValues<HintType>().Where(type => names.Contains(type.ToString())).ToHashSet();
    }

    // saves nothing for an account with no profile
    public async Task DismissAsync(string userId, IReadOnlyCollection<HintType> hints)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT dismiss_account_hints(@UserId, @Hints)",
            new { UserId = userId, Hints = Names(hints) }
        );
    }

    public async Task ShowAsync(string userId, IReadOnlyCollection<HintType> hints)
    {
        await using var connection = platformDataSource.CreateConnection();

        await connection.ExecuteAsync(
            "SELECT show_account_hints(@UserId, @Hints)",
            new { UserId = userId, Hints = Names(hints) }
        );
    }

    private static string[] Names(IReadOnlyCollection<HintType> hints) => [.. hints.Select(hint => hint.ToString())];
}
