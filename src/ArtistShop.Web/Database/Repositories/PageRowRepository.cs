namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
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
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CollectionsPageNeedsGrid))
        {
            throw new CollectionsPageNeedsGridException(exception.MessageText, exception);
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
    public async Task SetLayoutAsync(PageRowId id, int slotCount, RowWidths widths)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_page_row_layout(@Id, @SlotCount, @Widths)",
                new
                {
                    Id = id.Value,
                    SlotCount = slotCount,
                    Widths = (int)widths,
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
        var background = settings.Background;
        var backgroundSource = background is RowBackground.Image image ? image.Source : null;
        int? backgroundWorkId = backgroundSource is ImageSource.Work work ? work.WorkId.Value : null;
        var slideshow = background is RowBackground.Slideshow shown ? shown : null;

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_page_row_settings(@Id, @Height, @ContentWidth, @TopPadding, @BottomPadding, @SidePadding, @Gap, @Alignment, "
                    + "@ThemeId, @ThemePreset, @BackgroundType, @BackgroundColor, @BackgroundUploadedImageKey, @BackgroundWorkId, "
                    + "@BackgroundWorkImageKey, @BackgroundFit, @BackgroundSlideSource, @BackgroundSlideCollectionId, "
                    + "@BackgroundSlideInterval)",
                new
                {
                    Id = id.Value,
                    Height = (int)settings.Height,
                    ContentWidth = (int)settings.ContentWidth,
                    TopPadding = (int)settings.Padding.Top,
                    BottomPadding = (int)settings.Padding.Bottom,
                    SidePadding = (int)settings.Padding.Sides,
                    Gap = (int)settings.Gap,
                    Alignment = (int)settings.Alignment,
                    ThemeId = themeId,
                    ThemePreset = themePreset,
                    BackgroundType = (int)background.Type,
                    BackgroundColor = background.DrawnColor?.Hex,
                    BackgroundUploadedImageKey = backgroundSource is ImageSource.Library library ? library.StorageKey : null,
                    BackgroundWorkId = backgroundWorkId,
                    BackgroundWorkImageKey = backgroundSource is ImageSource.Work { StorageKey: var workImageKey } ? workImageKey : null,
                    BackgroundFit = (int?)background.PictureFit,
                    BackgroundSlideSource = (int?)slideshow?.Source.Type,
                    BackgroundSlideCollectionId = slideshow?.Source is SlideSource.WorksInCollection inCollection ? inCollection.CollectionId.Value : (int?)null,
                    BackgroundSlideInterval = (int?)slideshow?.Interval,
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.PageRowChangedSincePageLoad)
                || SqlErrors.IsThrown(exception, SqlStates.ThemeNoLongerExists)
                || SqlErrors.IsThrown(exception, SqlStates.UploadedImageNoLongerExists)
                || SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the pages with a row background showing any of these works, which deleting them clears
    public Task<List<string>> GetPagesWithBackgroundsShowingWorksAsync(IReadOnlyCollection<WorkId> ids) =>
        GetPagesWithBackgroundsShowingAsync([.. ids.Select(id => id.Value)], []);

    // the pages with a row background showing any of these library images, which deleting them clears
    public Task<List<string>> GetPagesWithBackgroundsShowingImagesAsync(IReadOnlyCollection<string> storageKeys) =>
        GetPagesWithBackgroundsShowingAsync([], [.. storageKeys]);

    private async Task<List<string>> GetPagesWithBackgroundsShowingAsync(int[] workIds, string[] storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        return [
            .. await connection.QueryAsync<string>(
                "SELECT * FROM get_pages_with_row_backgrounds_showing(@WorkIds, @StorageKeys)",
                new { WorkIds = workIds, StorageKeys = storageKeys }
            ),
        ];
    }
}
