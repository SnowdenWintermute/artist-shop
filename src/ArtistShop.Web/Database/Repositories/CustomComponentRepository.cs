namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
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

    // Off every page it's on as well. Leaving the blog page without a post list is refused
    public async Task DeleteAsync(CustomComponentId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync("SELECT delete_custom_component(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.BlogPageNeedsPostList))
        {
            throw new BlogPageNeedsPostListException(exception.MessageText, exception);
        }
    }

    // whether deleting it would leave the blog page without a post list
    public async Task<bool> IsBlogPagesOnlyPostListAsync(CustomComponentId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<bool>("SELECT is_blog_pages_only_post_list(@Id)", new { Id = id.Value });
    }

    // the image components showing any of these works, which deleting them deletes too
    public Task<List<CustomComponentOnPages>> GetImageComponentsShowingWorksAsync(IReadOnlyCollection<WorkId> ids) =>
        GetImageComponentsShowingAsync([.. ids.Select(id => id.Value)], []);

    // the image components showing any of these library images, which deleting them deletes too
    public Task<List<CustomComponentOnPages>> GetImageComponentsShowingImagesAsync(IReadOnlyCollection<string> storageKeys) =>
        GetImageComponentsShowingAsync([], [.. storageKeys]);

    private async Task<List<CustomComponentOnPages>> GetImageComponentsShowingAsync(int[] workIds, string[] storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ShowingRow>(
            "SELECT * FROM get_image_components_showing(@WorkIds, @StorageKeys)",
            new { WorkIds = workIds, StorageKeys = storageKeys }
        );

        return [.. rows.Select(row => new CustomComponentOnPages(new CustomComponentId(row.Id), row.Name, row.PageNames))];
    }

    private sealed class ShowingRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string[] PageNames { get; init; }
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
