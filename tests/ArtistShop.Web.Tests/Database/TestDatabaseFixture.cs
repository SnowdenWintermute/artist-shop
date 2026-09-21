using ArtistShop.Web.Database;
using Dapper;
using DbUp;
using Npgsql;

// xUnit v3: one instance for the whole test run, handed to any test class whose
// constructor asks for it. The migrations then run once rather than once per class
[assembly: AssemblyFixture(typeof(ArtistShop.Web.Tests.Database.TestDatabaseFixture))]

namespace ArtistShop.Web.Tests.Database;

public class TestDatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "artist_shop_tests";

    public string ConnectionString { get; }
    public NpgsqlDataSource DataSource { get; }

    public TestDatabaseFixture()
    {
        var developmentConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShop")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__ArtistShop is not set. Run `source env.sh` before `dotnet test`."
            );

        // same server and login as development, but a separate database
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(developmentConnectionString)
        {
            Database = DatabaseName,
        };

        ConnectionString = connectionStringBuilder.ConnectionString;
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }

    // IAsyncLifetime exists because constructors can't be async. xUnit calls this
    // after the constructor and before the first test runs
    public async ValueTask InitializeAsync()
    {
        // fresh every run, so edits to already-journaled scripts like 0001 take effect
        await DropDatabaseIfExistsAsync();
        EnsureDatabase.For.PostgresqlDatabase(ConnectionString);
        new SchemaMigrator().Upgrade(ConnectionString);

        // Program.cs does these for the app. They're global Dapper settings, and the tests
        // never run Program.cs, so they have to set them themselves
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private async Task DropDatabaseIfExistsAsync()
    {
        // the database being dropped can't be the one we're connected to, and every Postgres
        // server has one called postgres
        var serverConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(serverConnectionString);

        // FORCE disconnects other sessions, which would otherwise block the drop
        await connection.ExecuteAsync($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE);");
    }

    // dropped at the start rather than here, so a failed run's rows can still be inspected
    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}
