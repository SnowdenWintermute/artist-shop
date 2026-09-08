namespace ArtistShop.Web.Database;

using Microsoft.Data.SqlClient;

public class DatabaseInitializer
{
    public async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var databaseName = target.InitialCatalog;

        if (string.IsNullOrEmpty(databaseName))
        {
            throw new InvalidOperationException("Connection string names no database.");
        }

        // The database being created cannot be the one we connect to.
        target.InitialCatalog = "master";

        await using var connection = new SqlConnection(target.ConnectionString);

        await connection.OpenAsync();

        // CREATE DATABASE takes an identifier, not a parameter, so the name goes in through
        // QUOTENAME rather than string concatenation.
        await using var command = new SqlCommand(
            """
            IF DB_ID(@databaseName) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                EXEC sp_executesql @sql;
            END
            """,
            connection
        );

        command.Parameters.AddWithValue("@databaseName", databaseName);

        await command.ExecuteNonQueryAsync();
    }
}
