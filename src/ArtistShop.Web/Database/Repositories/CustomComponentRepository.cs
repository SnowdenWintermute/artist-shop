namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
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

    // A new component, unframed, placed at the bottom of the slot in the same step. Npgsql sends a C# string
    // as text, so the settings are cast to jsonb in the call
    public async Task<CustomComponentId> AddToSlotAsync(RowSlotId slotId, string name, CustomComponentSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();
        var (json, version) = CustomComponentSettingsJson.Write(settings, new ComponentFrame.Off());

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

    public async Task UpdateAsync(CustomComponentId id, string name, CustomComponentSettings settings, ComponentFrame frame)
    {
        await using var connection = await database.OpenConnectionAsync();
        var (json, version) = CustomComponentSettingsJson.Write(settings, frame);

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
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CollectionsPageNeedsGrid))
        {
            throw new CollectionsPageNeedsGridException(exception.MessageText, exception);
        }
    }

    // Off every page it's on as well. Leaving the blog page without a post list, or the collections page
    // without a card grid of all collections, is refused
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
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CollectionsPageNeedsGrid))
        {
            throw new CollectionsPageNeedsGridException(exception.MessageText, exception);
        }
    }

    // whether it sits in an edge-to-edge row, where an image component loads a full-bleed size
    public async Task<bool> IsInEdgeToEdgeRowAsync(CustomComponentId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<bool>("SELECT is_custom_component_in_edge_to_edge_row(@Id)", new { Id = id.Value });
    }

    // the image components showing any of these works, which deleting them deletes too
    public Task<List<CustomComponentOnPages>> GetImageComponentsShowingWorksAsync(IReadOnlyCollection<WorkId> ids) =>
        GetImageComponentsShowingAsync([.. ids.Select(id => id.Value)], []);

    // the image components showing any of these library images, which deleting them deletes too
    public Task<List<CustomComponentOnPages>> GetImageComponentsShowingImagesAsync(IReadOnlyCollection<string> storageKeys) =>
        GetImageComponentsShowingAsync([], [.. storageKeys]);

    // the components linking to any of these, which deleting them deletes or takes the link from
    public Task<LinkingComponents> GetLinkingToPagesAsync(IReadOnlyCollection<CustomPageId> ids) =>
        GetLinkingToAsync([.. ids.Select(id => id.Value)], [], [], []);

    public Task<LinkingComponents> GetLinkingToWorksAsync(IReadOnlyCollection<WorkId> ids) =>
        GetLinkingToAsync([], [.. ids.Select(id => id.Value)], [], []);

    public Task<LinkingComponents> GetLinkingToCollectionsAsync(IReadOnlyCollection<CollectionId> ids) =>
        GetLinkingToAsync([], [], [.. ids.Select(id => id.Value)], []);

    public Task<LinkingComponents> GetLinkingToPostsAsync(IReadOnlyCollection<PostId> ids) =>
        GetLinkingToAsync([], [], [], [.. ids.Select(id => id.Value)]);

    private async Task<LinkingComponents> GetLinkingToAsync(int[] pageIds, int[] workIds, int[] collectionIds, int[] postIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = (
            await connection.QueryAsync<LinkingRow>(
                "SELECT * FROM get_custom_components_linking_to(@PageIds, @WorkIds, @CollectionIds, @PostIds)",
                new { PageIds = pageIds, WorkIds = workIds, CollectionIds = collectionIds, PostIds = postIds }
            )
        ).ToLookup(row => row.Type is CustomComponentType.Button, row => new CustomComponentOnPages(new CustomComponentId(row.Id), row.Name, row.PageNames));

        return new LinkingComponents([.. rows[true]], [.. rows[false]]);
    }

    private sealed class LinkingRow
    {
        public required int Id { get; init; }
        public required CustomComponentType Type { get; init; }
        public required string Name { get; init; }
        public required string[] PageNames { get; init; }
    }

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
        public required bool IsBlogPagesOnlyPostList { get; init; }
        public required bool IsCollectionsPagesOnlyGrid { get; init; }

        public CustomComponentInUse ToComponentInUse() =>
            new(
                new CustomComponent(
                    new CustomComponentId(Id),
                    Name,
                    CustomComponentSettingsJson.Read(Type, SettingsVersion, Settings),
                    CustomComponentSettingsJson.ReadFrame(Settings)
                ),
                PageNames,
                UpdatedAt,
                IsBlogPagesOnlyPostList,
                IsCollectionsPagesOnlyGrid
            );
    }
}
