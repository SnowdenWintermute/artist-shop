namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

// the website's component library
public class CustomComponentRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_custom_components_name";

    public async Task<List<CustomComponentInUse>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ComponentRow>("SELECT * FROM get_all_custom_components()");

        return [.. rows.Select(row => row.ToComponentInUse())];
    }

    public async Task<CustomComponentInUse?> GetAsync(CustomComponentId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<ComponentRow>(
            "SELECT * FROM get_custom_component(@Id)",
            new { Id = id.Value }
        );

        return row?.ToComponentInUse();
    }

    // A new component, placed at the bottom of the slot in the same step. Npgsql sends a C# string
    // as text, so the settings are cast to jsonb in the call
    public async Task<CustomComponentId> AddToSlotAsync(RowSlotId slotId, string name, CustomComponentSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();
        var (json, version) = CustomComponentSettingsJson.Write(settings);

        try
        {
            return new CustomComponentId(
                await connection.QuerySingleAsync<int>(
                    "SELECT add_custom_component_to_slot(@SlotId, @Type, @Name, CAST(@Settings AS jsonb), @Version)",
                    new
                    {
                        SlotId = slotId.Value,
                        Type = (int)settings.Type,
                        Name = name,
                        Settings = json,
                        Version = version,
                    }
                )
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.RowSlotChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task UpdateAsync(CustomComponentId id, string name, CustomComponentSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();
        var (json, version) = CustomComponentSettingsJson.Write(settings);

        try
        {
            await connection.ExecuteAsync(
                "SELECT update_custom_component(@Id, @Name, CAST(@Settings AS jsonb), @Version)",
                new
                {
                    Id = id.Value,
                    Name = name,
                    Settings = json,
                    Version = version,
                }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CustomComponentNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the images uploaded into text components that their text still names, for the orphan sweep
    public async Task<HashSet<string>> GetAllImageStorageKeysAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var storageKeys = await connection.QueryAsync<string>("SELECT * FROM get_all_custom_component_image_storage_keys()");

        return [.. storageKeys];
    }

    private sealed class ComponentRow
    {
        public required int Id { get; init; }
        public required CustomComponentType Type { get; init; }
        public required string Name { get; init; }
        public required string Settings { get; init; }
        public required int SettingsVersion { get; init; }
        public required DateTimeOffset UpdatedAt { get; init; }
        public required string[] PageNames { get; init; }

        public CustomComponentInUse ToComponentInUse() =>
            new(
                new CustomComponent(new CustomComponentId(Id), Name, CustomComponentSettingsJson.Read(Type, SettingsVersion, Settings)),
                PageNames,
                UpdatedAt
            );
    }
}
