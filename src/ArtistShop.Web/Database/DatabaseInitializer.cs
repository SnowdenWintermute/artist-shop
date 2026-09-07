namespace ArtistShop.Web.Database;

using Microsoft.Data.SqlClient;

public class DatabaseInitializer(IConfiguration configuration)
{
    private readonly string _connectionString = CreateConnectionString(configuration);

    private static string CreateConnectionString(IConfiguration configuration)
    {
        var password =
            configuration["MSSQL_SA_PASSWORD"]
            ?? throw new InvalidOperationException(
                "MSSQL_SA_PASSWORD environment variable is missing."
            );

        return new SqlConnectionStringBuilder
        {
            DataSource = "localhost,1433",
            UserID = "sa",
            Password = password,
            TrustServerCertificate = true,
        }.ConnectionString;
    }

    public async Task EnsureDatabaseExistsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            IF DB_ID(N'ArtistShop') IS NULL
            BEGIN
                CREATE DATABASE [ArtistShop];
            END
            """,
            connection
        );

        await command.ExecuteNonQueryAsync();
    }
}
