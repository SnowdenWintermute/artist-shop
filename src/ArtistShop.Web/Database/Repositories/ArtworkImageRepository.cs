namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using Dapper;

public class ArtworkImageRepository(SqlConnectionFactory connectionFactory)
{
    // since we're using this to determine if an image exists, we
    // pick hash set
    public async Task<HashSet<string>> GetAllRelativePathsAsync()
    {
        await using var connection = connectionFactory.Create();

        // when a result set has exactly one column,
        // QueryAsync maps each row to a string, if it
        // had more columns we would create a class to
        // represent that row's data in C#
        var relativePaths = await connection.QueryAsync<string>(
            "dbo.GetAllArtworkImageRelativePaths",
            commandType: CommandType.StoredProcedure
        );

        return [.. relativePaths];
    }
}
