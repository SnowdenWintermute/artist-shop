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

    // every way these find the row or slot no longer as the page showed it
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
    }
}
