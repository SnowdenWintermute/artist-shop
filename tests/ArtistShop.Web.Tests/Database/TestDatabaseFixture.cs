using ArtistShop.Web.Database;
using Dapper;
using Microsoft.Data.SqlClient;

// xUnit v3: one instance for the whole test run, handed to any test class whose
// constructor asks for it. The migrations then run once rather than once per class
[assembly: AssemblyFixture(typeof(ArtistShop.Web.Tests.Database.TestDatabaseFixture))]

namespace ArtistShop.Web.Tests.Database;

public class TestDatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "ArtistShopTests";

    public string ConnectionString { get; }
    public SqlConnectionFactory ConnectionFactory { get; }

    public TestDatabaseFixture()
    {
        var developmentConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShop")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__ArtistShop is not set. Run `source env.sh` before `dotnet test`."
            );

        // same server and login as development, but a separate database
        var connectionStringBuilder = new SqlConnectionStringBuilder(developmentConnectionString)
        {
            InitialCatalog = DatabaseName,
        };

        ConnectionString = connectionStringBuilder.ConnectionString;
        ConnectionFactory = new SqlConnectionFactory(ConnectionString);
    }

    // IAsyncLifetime exists because constructors can't be async. xUnit calls this
    // after the constructor and before the first test runs
    public async ValueTask InitializeAsync()
    {
        // fresh every run, so edits to already-journaled scripts like 0001 take effect
        await DropDatabaseIfExistsAsync();
        await new DatabaseInitializer().EnsureDatabaseExistsAsync(ConnectionString);
        new SchemaMigrator().Upgrade(ConnectionString);

        // Program.cs does this for the app. It's a global Dapper setting, and the tests
        // never run Program.cs, so they have to register it themselves
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    private async Task DropDatabaseIfExistsAsync()
    {
        var masterConnectionString = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        await using var connection = new SqlConnection(masterConnectionString);

        // SINGLE_USER WITH ROLLBACK IMMEDIATE disconnects other sessions, which would block the drop
        await connection.ExecuteAsync(
            $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE {DatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {DatabaseName};
            END
            """
        );
    }

    // dropped at the start rather than here, so a failed run's rows can still be inspected
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
