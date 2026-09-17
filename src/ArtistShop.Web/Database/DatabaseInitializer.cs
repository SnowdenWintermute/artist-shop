namespace ArtistShop.Web.Database;

using Microsoft.Data.SqlClient;

public class DatabaseInitializer
{
    // Case-insensitive and accent-sensitive; _SC makes functions like LEN count an emoji as one
    // character. Name matching and the UNIQUE constraints on names depend on it, so it's pinned
    // here instead of taken from the server's default
    public const string Collation = "Latin1_General_100_CI_AS_SC";

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
        // QUOTENAME rather than string concatenation. The collation is our own constant, so it
        // can be pasted in as it is.
        await using var command = new SqlCommand(
            $"""
            IF DB_ID(@databaseName) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName) + N' COLLATE {Collation}';
                EXEC sp_executesql @sql;
            END
            """,
            connection
        );

        command.Parameters.AddWithValue("@databaseName", databaseName);

        await command.ExecuteNonQueryAsync();
    }

    // A database created by someone else (a host's control panel, say) skips the CREATE above,
    // so its collation has to be checked separately
    public async Task VerifyCollationAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            // DATABASEPROPERTYEX returns sql_variant, which SqlClient can't turn into a string by itself
            "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));",
            connection
        );

        var collation = (string?)await command.ExecuteScalarAsync();

        if (collation != Collation)
        {
            // ALTER DATABASE ... COLLATE only changes the default for new columns, not existing ones
            throw new InvalidOperationException(
                $"Database {connection.Database} uses collation {collation}, but the app needs {Collation}. "
                    + "Recreate the database with that collation."
            );
        }
    }
}
