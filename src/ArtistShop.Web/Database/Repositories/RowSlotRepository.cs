namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

// a row's slots and the components placed in them
public class RowSlotRepository(SiteDatabase database)
{
    private const string UniquePlacementConstraint = "unique_slot_placements_slot_component";

    // An empty slot at the row's right end. slotCount is how many slots the page saw; the row
    // takes the first split for one more
    public Task AddAsync(PageRowId rowId, int slotCount) =>
        ExecuteAsync(
            "SELECT add_row_slot(@RowId, @SlotCount, @Widths)",
            new
            {
                RowId = rowId.Value,
                SlotCount = slotCount,
                Widths = (int)RowWidthChoices.For(slotCount + 1)[0],
            }
        );

    // its components stay in the library; the row takes the first split for one fewer
    public Task DeleteAsync(PageRowId rowId, RowSlotId id, int slotCount) =>
        ExecuteAsync(
            "SELECT delete_row_slot(@RowId, @Id, @SlotCount, @Widths)",
            new
            {
                RowId = rowId.Value,
                Id = id.Value,
                SlotCount = slotCount,
                Widths = (int)RowWidthChoices.For(slotCount - 1)[0],
            }
        );

    // slotIds is every slot in the row, left to right in the new order; the widths stay by position
    public Task ReorderAsync(PageRowId rowId, IReadOnlyList<RowSlotId> slotIds) =>
        ExecuteAsync(
            "SELECT reorder_row_slots(@RowId, @SlotIds)",
            new { RowId = rowId.Value, SlotIds = slotIds.Select(id => id.Value).ToArray() }
        );

    // Into another row at that place, with its stack. The slot counts are how many slots the page saw in each row;
    // both rows take the first split for their new count, and a row left with none gets an empty slot
    public Task MoveAsync(RowSlotId id, PageRowId fromRowId, int fromSlotCount, PageRowId toRowId, int toSlotCount, int toIndex) =>
        ExecuteAsync(
            "SELECT move_row_slot(@Id, @FromRowId, @FromSlotCount, @FromWidths, @ToRowId, @ToSlotCount, @ToWidths, @ToIndex)",
            new
            {
                Id = id.Value,
                FromRowId = fromRowId.Value,
                FromSlotCount = fromSlotCount,
                FromWidths = (int)RowWidthChoices.For(Math.Max(fromSlotCount - 1, 1))[0],
                ToRowId = toRowId.Value,
                ToSlotCount = toSlotCount,
                ToWidths = (int)RowWidthChoices.For(toSlotCount + 1)[0],
                ToIndex = toIndex,
            }
        );

    // from one slot's stack into another's at that place
    public Task MovePlacementAsync(RowSlotId fromId, CustomComponentId componentId, RowSlotId toId, int toIndex) =>
        ExecuteAsync(
            "SELECT move_slot_placement(@FromId, @ComponentId, @ToId, @ToIndex)",
            new
            {
                FromId = fromId.Value,
                ComponentId = componentId.Value,
                ToId = toId.Value,
                ToIndex = toIndex,
            }
        );

    // at the bottom of the slot's stack
    public Task PlaceAsync(RowSlotId id, CustomComponentId componentId) =>
        ExecuteAsync("SELECT place_custom_component(@Id, @ComponentId)", new { Id = id.Value, ComponentId = componentId.Value });

    // the component stays in the library
    public Task RemovePlacementAsync(RowSlotId id, CustomComponentId componentId) =>
        ExecuteAsync("SELECT remove_slot_placement(@Id, @ComponentId)", new { Id = id.Value, ComponentId = componentId.Value });

    // componentIds is every component in the slot, in the new order
    public Task ReorderPlacementsAsync(RowSlotId id, IReadOnlyList<CustomComponentId> componentIds) =>
        ExecuteAsync(
            "SELECT reorder_slot_placements(@Id, @ComponentIds)",
            new { Id = id.Value, ComponentIds = componentIds.Select(componentId => componentId.Value).ToArray() }
        );

    // every way these find the row or slot no longer as the page showed it, or would leave the blog
    // page without its post list or the collections page without its card grid of all collections
    private async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(sql, parameters);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.PageRowChangedSincePageLoad)
                || SqlErrors.IsThrown(exception, SqlStates.RowSlotChangedSincePageLoad)
                || SqlErrors.IsUniqueConstraintViolation(exception, UniquePlacementConstraint)
                || exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
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
}
