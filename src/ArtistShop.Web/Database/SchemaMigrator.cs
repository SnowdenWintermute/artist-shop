namespace ArtistShop.Web.Database;

using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

// Migrations run once each, journaled in the database; procedures are re-created on every run, so
// an edited one always takes effect. Each kind of database has its own folders
public class SchemaMigrator(string scriptsFolder, string proceduresFolder)
{
    // a site's own database: its catalog and posts
    public static readonly SchemaMigrator Site = new(".Database.Scripts.", ".Database.Procedures.");

    // the platform database: the sites and their hosts
    public static readonly SchemaMigrator Platform = new(
        ".Database.Platform.Scripts.",
        ".Database.Platform.Procedures."
    );

    public void Upgrade(string connectionString)
    {
        var migrations = DeployChanges
            .To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsMigration)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        Run(migrations);

        var procedures = DeployChanges
            .To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsProcedure)
            .WithTransactionPerScript()
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
