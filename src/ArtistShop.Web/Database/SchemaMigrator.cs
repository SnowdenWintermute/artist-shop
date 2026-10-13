namespace ArtistShop.Web.Database;

using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

// Migrations run once each, journaled in the database; procedures are re-created on every run, so
// an edited one always takes effect. Each kind of database has its own folders
public class SchemaMigrator(string scriptsFolder, string proceduresFolder)
{
    // a site's own schema: its catalog and posts
    public static readonly SchemaMigrator Site = new(".Database.Scripts.", ".Database.Procedures.");

    // the platform database: the sites and their hosts
    public static readonly SchemaMigrator Platform = new(
        ".Database.Platform.Scripts.",
        ".Database.Platform.Procedures."
    );

    // schema is where the list of migrations already run is kept: public for the platform, and a
    // site's own schema for a site, so each site has its own list. Named here, since otherwise DbUp
    // takes the connection string's whole Search Path as one schema's name, and makes it.
    // Each kind of script runs in one transaction rather than one per script: every commit waits for
    // the disk, and with one per procedure that made bringing a site up to date take about seven
    // times as long, at every startup for every site. A script that fails undoes the others with it
    public void Upgrade(string connectionString, string schema)
    {
        var migrations = DeployChanges
            .To.PostgresqlDatabase(connectionString, schema)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsMigration)
            .WithTransaction()
            .LogToConsole()
            .Build();

        Run(migrations);

        var procedures = DeployChanges
            .To.PostgresqlDatabase(connectionString, schema)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsProcedure)
            .WithTransaction()
            .JournalTo(new NullJournal())
            .LogToConsole()
            .Build();

        Run(procedures);
    }

    // an embedded script's name is its path with dots, such as
    // ArtistShop.Web.Database.Platform.Scripts.0001_CreateSites.sql
    private bool IsMigration(string resourceName) => resourceName.Contains(scriptsFolder);

    private bool IsProcedure(string resourceName) => resourceName.Contains(proceduresFolder);

    private static void Run(UpgradeEngine upgrader)
    {
        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database upgrade script failed at '{result.ErrorScript?.Name}'",
                result.Error
            );
        }
    }
}
