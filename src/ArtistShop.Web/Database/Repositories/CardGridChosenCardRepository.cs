namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

// card grids' custom lists: the collections and works each one shows, in the artist's order
public class CardGridChosenCardRepository(SiteDatabase database)
{
    // every card on the list, the ones a visitor wouldn't see included
    public async Task<List<ChosenCard>> GetAsync(CustomComponentId componentId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ChosenCardRow>(
            "SELECT * FROM get_card_grid_chosen_cards(@ComponentId)",
            new { ComponentId = componentId.Value }
        );

        return [.. rows.Select(row => row.ToChosenCard())];
    }

    // at the end of the list, in the order given
    public Task AddCollectionsAsync(CustomComponentId componentId, IReadOnlyList<CollectionId> collectionIds) =>
        AddAsync("add_card_grid_chosen_collections", componentId, [.. collectionIds.Select(id => id.Value)]);

    // at the end of the list, in the order given
    public Task AddWorksAsync(CustomComponentId componentId, IReadOnlyList<WorkId> workIds) =>
        AddAsync("add_card_grid_chosen_works", componentId, [.. workIds.Select(id => id.Value)]);

    private async Task AddAsync(string function, CustomComponentId componentId, int[] ids)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync($"SELECT {function}(@ComponentId, @Ids)", new { ComponentId = componentId.Value, Ids = ids });
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CustomComponentNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task RemoveAsync(CustomComponentId componentId, ChosenCardId cardId)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT remove_card_grid_chosen_card(@ComponentId, @CardId)",
            new { ComponentId = componentId.Value, CardId = cardId.Value }
        );
    }

    // cardIds is every card on the list, in the new order
    public async Task ReorderAsync(CustomComponentId componentId, IReadOnlyList<ChosenCardId> cardIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_card_grid_chosen_cards(@ComponentId, @CardIds)",
                new { ComponentId = componentId.Value, CardIds = (int[])[.. cardIds.Select(id => id.Value)] }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CustomComponentNoLongerExists)
                  || SqlErrors.IsThrown(exception, SqlStates.CardGridChosenCardsChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    private sealed class ChosenCardRow
    {
        public required int Id { get; init; }
        public int? CollectionId { get; init; }
        public string? CollectionName { get; init; }
        public string? CollectionSlug { get; init; }
        public int? CollectionWorkCount { get; init; }
        public int? WorkId { get; init; }
        public string? WorkName { get; init; }
        public string? WorkSlug { get; init; }
        public string? ImageStorageKey { get; init; }
        public string? ImageOriginalFileName { get; init; }
        public int? ImageWidth { get; init; }
        public int? ImageHeight { get; init; }
        public string? ImageBlurDataUri { get; init; }

        private WorkImage? Image =>
            this is { ImageStorageKey: string storageKey, ImageWidth: int width, ImageHeight: int height }
                ? new WorkImage(storageKey, ImageOriginalFileName, width, height, ImageBlurDataUri)
                : null;

        // the table's check holds exactly one of the two
        public ChosenCard ToChosenCard() =>
            new(
                new ChosenCardId(Id),
                this switch
                {
                    { CollectionId: int collectionId, CollectionName: string name, CollectionSlug: string slug } =>
                        new GridCard.OfCollection(
                            new CollectionWithCover(new CollectionId(collectionId), new CollectionName(name), new CollectionSlug(slug),
                                // a collection a visitor wouldn't see has no works with images
                                CollectionWorkCount ?? 0, Image)
                        ),
                    { WorkId: int workId, WorkName: string name, WorkSlug: string slug } =>
                        new GridCard.OfWork(new WorkId(workId), new WorkName(name), new WorkSlug(slug), Image),
                    _ => throw new InvalidOperationException($"Chosen card {Id} is neither a collection nor a work."),
                }
            );
    }
}
