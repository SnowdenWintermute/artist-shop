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
    private const string PlatformDatabaseName = "artist_shop_platform_tests";

    // the site databases the tests make, never the app's artist_shop_site_…
    public const string SiteDatabaseNamePrefix = "artist_shop_tests_site_";

    // a site's database, for the repositories that work in one
    public string ConnectionString { get; }
    public NpgsqlDataSource DataSource { get; }

    // the platform database, for the tests of the sites and their hosts
    public string PlatformConnectionString { get; }
    public NpgsqlDataSource PlatformDataSource { get; }

    public TestDatabaseFixture()
    {
        var developmentConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShopPlatform")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__ArtistShopPlatform is not set. Run `source env.sh` before `dotnet test`."
            );

        // same server and login as development, but separate databases
        ConnectionString = new NpgsqlConnectionStringBuilder(developmentConnectionString)
        {
            Database = DatabaseName,
        }.ConnectionString;
        DataSource = SiteDataSource.Create(ConnectionString);

        PlatformConnectionString = new NpgsqlConnectionStringBuilder(developmentConnectionString)
        {
            Database = PlatformDatabaseName,
        }.ConnectionString;
        PlatformDataSource = NpgsqlDataSource.Create(PlatformConnectionString);
    }

    // IAsyncLifetime exists because constructors can't be async. xUnit calls this
    // after the constructor and before the first test runs
    public async ValueTask InitializeAsync()
    {
        // fresh every run, so edits to already-journaled scripts like 0001 take effect
        await DropTestDatabasesAsync();

        EnsureDatabase.For.PostgresqlDatabase(ConnectionString);
        SchemaMigrator.Site.Upgrade(ConnectionString);

        EnsureDatabase.For.PostgresqlDatabase(PlatformConnectionString);
        SchemaMigrator.Platform.Upgrade(PlatformConnectionString);

        // Program.cs does these for the app. They're global Dapper settings, and the tests
        // never run Program.cs, so they have to set them themselves
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    // the two above, and every site database a previous run made
    private async Task DropTestDatabasesAsync()
    {
        // the database being dropped can't be the one we're connected to, and every Postgres
        // server has one called postgres
        var serverConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(serverConnectionString);

        // LIKE's _ matches any one character, so it's escaped to mean itself
        var siteDatabaseNames = await connection.QueryAsync<string>(
            "SELECT datname FROM pg_database WHERE datname LIKE @Pattern",
            new { Pattern = SiteDatabaseNamePrefix.Replace("_", "\\_") + "%" }
        );

        // FORCE disconnects other sessions, which would otherwise block the drop. A database name
        // is an identifier, not a value, so it can't be a parameter; these come from the constants
        // above and from names that match their prefix
        foreach (var name in (string[])[DatabaseName, PlatformDatabaseName, .. siteDatabaseNames])
        {
            await connection.ExecuteAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE);");
        }
    }

    // dropped at the start rather than here, so a failed run's rows can still be inspected
    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await PlatformDataSource.DisposeAsync();
    }
}
