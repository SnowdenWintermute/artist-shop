namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

// the website's component library
public class CustomComponentRepository(SiteDatabase database)
{
    public async Task<List<CustomComponentInUse>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ComponentRow>("SELECT * FROM get_all_custom_components()");

        return
        [
            .. rows.Select(row => new CustomComponentInUse(
                new CustomComponent(
                    new CustomComponentId(row.Id),
                    row.Name,
                    CustomComponentSettingsJson.Read(row.Type, row.SettingsVersion, row.Settings)
                ),
                row.PageNames
            )),
        ];
    }

    // A new component, placed at the bottom of the slot in the same step. Npgsql sends a C# string
    // as text, so the settings are cast to jsonb in the call
    public async Task<CustomComponentId> AddToSlotAsync(RowSlotId slotId, CustomComponentSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();
        var (json, version) = CustomComponentSettingsJson.Write(settings);

        try
        {
            return new CustomComponentId(
                await connection.QuerySingleAsync<int>(
                    "SELECT add_custom_component_to_slot(@SlotId, @Type, CAST(@Settings AS jsonb), @Version)",
                    new
                    {
                        SlotId = slotId.Value,
                        Type = (int)settings.Type,
                        Settings = json,
                        Version = version,
                    }
                )
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.RowSlotChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    private sealed class ComponentRow
    {
        public required int Id { get; init; }
        public required CustomComponentType Type { get; init; }
        public required string? Name { get; init; }
        public required string Settings { get; init; }
        public required int SettingsVersion { get; init; }
        public required string[] PageNames { get; init; }
    }
}
