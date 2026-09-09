namespace ArtistShop.Web.Database;

using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

public class SchemaMigrator
{
    public void Upgrade(string connectionString)
    {
        var migrations = DeployChanges
            .To.SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsMigration)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        Run(migrations);

        var procedures = DeployChanges
            .To.SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly, IsProcedure)
            .WithTransactionPerScript()
            .JournalTo(new NullJournal())
            .LogToConsole()
            .Build();

        Run(procedures);
    }

    private static bool IsMigration(string resourceName) =>
        resourceName.Contains(".Database.Scripts.");

    private static bool IsProcedure(string resourceName) =>
        resourceName.Contains(".Database.Procedures.");

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
