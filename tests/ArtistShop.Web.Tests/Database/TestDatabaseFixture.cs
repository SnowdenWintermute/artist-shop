using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using Dapper;
using DbUp;
using Npgsql;

// xUnit v3: one instance for the whole test run, handed to any test class whose
// constructor asks for it. The migrations then run once rather than once per class
[assembly: AssemblyFixture(typeof(ArtistShop.Web.Tests.Database.TestDatabaseFixture))]

namespace ArtistShop.Web.Tests.Database;

public class TestDatabaseFixture : IAsyncLifetime
{
    // the platform's tables and every site schema the tests make, as in the app
    private const string DatabaseName = "artist_shop_tests";

    public string PlatformConnectionString { get; }
    public NpgsqlDataSource PlatformDataSource { get; }

    // the pool every site's schema is reached through, as in the app
    public NpgsqlDataSource SitesDataSource { get; }
    public SiteDatabases SiteDatabases { get; }

    // A site's schema, for the repositories that work in one. Made as InitializeAsync runs, since
    // its id comes from the database
    public SiteDatabase Site =>
        _site ?? throw new InvalidOperationException("The test site is made as the fixture starts.");

    private SiteDatabase? _site;

    // the databases of the whole app that TestApp runs, dropped at the start of each run with the
    // other. Its site schemas are inside its platform database, so they go with it
    public const string AppPlatformDatabaseName = "artist_shop_tests_app_platform";
    public const string AppIdentityDatabaseName = "artist_shop_tests_app_identity";

    public TestDatabaseFixture()
    {
        PlatformConnectionString = ConnectionStringFor(DatabaseName);
        PlatformDataSource = NpgsqlDataSource.Create(PlatformConnectionString);
        SitesDataSource = SiteDataSource.Create(
            PlatformConnectionString,
            new SiteDatabaseSettings { MaximumPoolSize = 5, ConnectionIdleLifetime = TimeSpan.FromSeconds(10) }
        );
        SiteDatabases = new SiteDatabases(SitesDataSource);
    }

    // a database on development's server, with its login
    public static string ConnectionStringFor(string databaseName)
    {
        var developmentConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShopPlatform")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__ArtistShopPlatform is not set. Run `source env.sh` before `dotnet test`."
            );

        return new NpgsqlConnectionStringBuilder(developmentConnectionString) { Database = databaseName }.ConnectionString;
    }

    // IAsyncLifetime exists because constructors can't be async. xUnit calls this
    // after the constructor and before the first test runs
    public async ValueTask InitializeAsync()
    {
        // fresh every run, so edits to already-journaled scripts like 0001 take effect
        await DropTestDatabasesAsync();

        EnsureDatabase.For.PostgresqlDatabase(PlatformConnectionString);
        SchemaMigrator.Platform.Upgrade(PlatformConnectionString, "public");

        // only the schema, with no row in sites: the repository tests need a site's tables and
        // functions, not its hosts or folders
        var siteId = await new SiteRepository(PlatformDataSource).ReserveIdAsync();
        new SiteSchemas(PlatformConnectionString).Migrate(siteId);
        _site = SiteDatabases.For(siteId);

        // Program.cs does these for the app. They're global Dapper settings, and the tests
        // never run Program.cs, so they have to set them themselves
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private static async Task DropTestDatabasesAsync()
    {
        // the database being dropped can't be the one we're connected to, and every Postgres
        // server has one called postgres
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres"));

        // FORCE disconnects other sessions, which would otherwise block the drop. A database name
        // is an identifier, not a value, so it can't be a parameter; these come from the constants
        // above
        foreach (var name in (string[])[DatabaseName, AppPlatformDatabaseName, AppIdentityDatabaseName])
        {
            await connection.ExecuteAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE);");
        }
    }

    // dropped at the start rather than here, so a failed run's rows can still be inspected
    public async ValueTask DisposeAsync()
    {
        await SitesDataSource.DisposeAsync();
        await PlatformDataSource.DisposeAsync();
    }
}
