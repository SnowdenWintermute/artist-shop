namespace ArtistShop.Web.Database;

using DbUp;

public class SchemaMigrator
{
    public void Upgrade(string connectionString)
    {
        var upgrader = DeployChanges
            .To.SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(SchemaMigrator).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

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
