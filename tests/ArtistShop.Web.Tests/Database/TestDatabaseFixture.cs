using ArtistShop.Web.Database;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ArtistShop.Web.Tests.Database;

// When a second test class needs the database, turn this into an assembly fixture
// (created once for the whole test run). Otherwise two classes running in parallel
// would each run the migrations at the same moment
public class TestDatabaseFixture : IAsyncLifetime
{
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
            InitialCatalog = "ArtistShopTests",
        };

        ConnectionString = connectionStringBuilder.ConnectionString;
        ConnectionFactory = new SqlConnectionFactory(ConnectionString);
    }

    // IAsyncLifetime exists because constructors can't be async. xUnit calls this
    // after the constructor and before the first test runs
    public async ValueTask InitializeAsync()
    {
        await new DatabaseInitializer().EnsureDatabaseExistsAsync(ConnectionString);
        new SchemaMigrator().Upgrade(ConnectionString);

        // Program.cs does this for the app. It's a global Dapper setting, and the tests
        // never run Program.cs, so they have to register it themselves
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    // called after the last test. The database is deliberately kept between runs: every
    // test uses brand new storage keys and its own directory, so leftover rows from earlier
    // runs can't affect any result
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
