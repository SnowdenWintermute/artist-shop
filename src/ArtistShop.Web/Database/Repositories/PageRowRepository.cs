namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;
using Npgsql;

public class PageRowRepository(SiteDatabase database)
{
    // a row at the bottom of the page, with one empty slot
    public async Task<PageRowId> AddAsync(CustomPageId pageId)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            return new PageRowId(await connection.QuerySingleAsync<int>("SELECT add_page_row(@PageId)", new { PageId = pageId.Value }));
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CustomPageNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // its components stay in the library
    public async Task DeleteAsync(PageRowId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync("SELECT delete_page_row(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.BlogPageNeedsPostList))
        {
            throw new BlogPageNeedsPostListException(exception.MessageText, exception);
        }
    }

    // rowIds is every row on the page, in the new order
    public async Task ReorderAsync(CustomPageId pageId, IReadOnlyList<PageRowId> rowIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_page_rows(@PageId, @RowIds)",
                new { PageId = pageId.Value, RowIds = rowIds.Select(id => id.Value).ToArray() }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.PageRowsChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // slotCount is how many slots the page saw, which widths was chosen for
    public async Task SetLayoutAsync(PageRowId id, int slotCount, RowWidths widths, RowAlignment alignment)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_page_row_layout(@Id, @SlotCount, @Widths, @Alignment)",
                new
                {
                    Id = id.Value,
                    SlotCount = slotCount,
                    Widths = (int)widths,
                    Alignment = (int)alignment,
                }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.PageRowChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task SetSettingsAsync(PageRowId id, RowSettings settings)
    {
        await using var connection = await database.OpenConnectionAsync();
        int? themeId = settings.Theme is ThemeKey.Saved saved ? saved.Id.Value : null;
        ThemePreset? themePreset = settings.Theme is ThemeKey.Preset preset ? preset.Value : null;
        string? backgroundColor = settings.Background is RowBackground.Color color ? color.Value.Hex : null;

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_page_row_settings(@Id, @Height, @ContentWidth, @TopPadding, @BottomPadding, @SidePadding, @Gap, "
                    + "@ThemeId, @ThemePreset, @BackgroundColor)",
                new
                {
                    Id = id.Value,
                    Height = (int)settings.Height,
                    ContentWidth = (int)settings.ContentWidth,
                    TopPadding = (int)settings.Padding.Top,
                    BottomPadding = (int)settings.Padding.Bottom,
                    SidePadding = (int)settings.Padding.Sides,
                    Gap = (int)settings.Gap,
                    ThemeId = themeId,
                    ThemePreset = themePreset,
                    BackgroundColor = backgroundColor,
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.PageRowChangedSincePageLoad)
                || SqlErrors.IsThrown(exception, SqlStates.ThemeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }
}
