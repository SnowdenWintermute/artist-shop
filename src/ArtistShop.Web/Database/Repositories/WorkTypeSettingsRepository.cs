namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;

public class WorkTypeSettingsRepository(SiteDatabase database)
{
    public async Task<WorkTypeSettings> GetAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<WorkTypeSettingsRow>("SELECT * FROM get_work_type_settings()");

        return new WorkTypeSettings(row.DimensionDisplay ?? WorkTypeSettings.Default.DimensionDisplay);
    }

    public async Task UpdateAsync(WorkTypeSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT update_work_type_settings(@DimensionDisplay)",
            new { DimensionDisplay = (int)settings.DimensionDisplay }
        );
    }

    private sealed class WorkTypeSettingsRow
    {
        public required DimensionDisplay? DimensionDisplay { get; init; }
    }
}
