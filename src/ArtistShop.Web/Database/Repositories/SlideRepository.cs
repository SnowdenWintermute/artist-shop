namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;
using Dapper;
using Npgsql;

// the chosen slides of carousels and row slideshows, in the artist's order
public class SlideRepository(SiteDatabase database)
{
    public async Task<List<ChosenSlide>> GetAsync(SlideOwner owner)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<SlideRow>("SELECT * FROM get_slides(@ComponentId, @RowId)", OwnerParameters.Of(owner));

        return [.. rows.Select(row => row.ToChosenSlide())];
    }

    // at the end of the list
    public async Task AddAsync(SlideOwner owner, ImageSource source)
    {
        await using var connection = await database.OpenConnectionAsync();
        var ids = OwnerParameters.Of(owner);

        try
        {
            await connection.ExecuteAsync(
                "SELECT add_slide(@ComponentId, @RowId, @UploadedImageKey, @WorkId, @WorkImageKey)",
                new
                {
                    ids.ComponentId,
                    ids.RowId,
                    UploadedImageKey = source is ImageSource.Library library ? library.StorageKey : null,
                    WorkId = source is ImageSource.Work work ? work.WorkId.Value : (int?)null,
                    WorkImageKey = source is ImageSource.Work { StorageKey: var workImageKey } ? workImageKey : null,
                }
            );
        }
        catch (PostgresException exception) when (IsOwnerGone(exception)
            || SqlErrors.IsThrown(exception, SqlStates.UploadedImageNoLongerExists)
            || SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task RemoveAsync(SlideOwner owner, SlideId slideId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var ids = OwnerParameters.Of(owner);

        await connection.ExecuteAsync(
            "SELECT remove_slide(@ComponentId, @RowId, @SlideId)",
            new { ids.ComponentId, ids.RowId, SlideId = slideId.Value }
        );
    }

    // slideIds is every one of the owner's slides, in the new order
    public async Task ReorderAsync(SlideOwner owner, IReadOnlyList<SlideId> slideIds)
    {
        await using var connection = await database.OpenConnectionAsync();
        var ids = OwnerParameters.Of(owner);

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_slides(@ComponentId, @RowId, @SlideIds)",
                new { ids.ComponentId, ids.RowId, SlideIds = (int[])[.. slideIds.Select(id => id.Value)] }
            );
        }
        catch (PostgresException exception) when (IsOwnerGone(exception)
            || SqlErrors.IsThrown(exception, SqlStates.SlidesChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the carousels and row slideshows with slides of any of these works, which deleting them takes out
    public Task<SlideOwnersShowing> GetOwnersShowingWorksAsync(IReadOnlyCollection<WorkId> ids) =>
        GetOwnersShowingAsync([.. ids.Select(id => id.Value)], []);

    // the same for library images
    public Task<SlideOwnersShowing> GetOwnersShowingImagesAsync(IReadOnlyCollection<string> storageKeys) =>
        GetOwnersShowingAsync([], [.. storageKeys]);

    private async Task<SlideOwnersShowing> GetOwnersShowingAsync(int[] workIds, string[] storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = (await connection.QueryAsync<OwnerShowingRow>(
            "SELECT * FROM get_slide_owners_showing(@WorkIds, @StorageKeys)",
            new { WorkIds = workIds, StorageKeys = storageKeys }
        )).ToList();

        // a row's page_names is its one page
        return new SlideOwnersShowing(
            [
                .. rows.Where(row => row.ComponentId is not null)
                    .Select(row => new CustomComponentOnPages(new CustomComponentId(Unwrap.Value(row.ComponentId)), Unwrap.Value(row.ComponentName), row.PageNames)),
            ],
            [.. rows.Where(row => row.ComponentId is null).SelectMany(row => row.PageNames)]
        );
    }

    private static bool IsOwnerGone(PostgresException exception) =>
        SqlErrors.IsThrown(exception, SqlStates.CustomComponentNoLongerExists)
        || SqlErrors.IsThrown(exception, SqlStates.PageRowChangedSincePageLoad);

    // the owner as the functions take it: one id, the other null
    private sealed record OwnerParameters(int? ComponentId, int? RowId)
    {
        public static OwnerParameters Of(SlideOwner owner) =>
            owner switch
            {
                SlideOwner.Component component => new(component.Id.Value, null),
                SlideOwner.Row row => new(null, row.Id.Value),
                _ => throw new InvalidOperationException($"{owner.GetType().Name} isn't a slide owner."),
            };
    }

    private sealed class OwnerShowingRow
    {
        public required int? ComponentId { get; init; }
        public required string? ComponentName { get; init; }
        public required string[] PageNames { get; init; }
    }

    private sealed class SlideRow
    {
        public required int Id { get; init; }
        public required string? UploadedImageKey { get; init; }
        public required int? WorkId { get; init; }
        public required string? WorkImageKey { get; init; }
        public required string? ImageStorageKey { get; init; }
        public required string? ImageOriginalFileName { get; init; }
        public required int? ImageWidth { get; init; }
        public required int? ImageHeight { get; init; }
        public required string? ImageBlurDataUri { get; init; }
        public required string? LibraryAltText { get; init; }
        public required string? WorkName { get; init; }
        public required string? WorkSlug { get; init; }
        public required int? ImagePlace { get; init; }
        public required bool? IsMainImage { get; init; }

        // the table's check holds exactly one of the library key and the work
        public ChosenSlide ToChosenSlide() =>
            new(
                new SlideId(Id),
                Unwrap.Value(ImageSource.Of(WorkId, WorkImageKey ?? UploadedImageKey)),
                this switch
                {
                    { ImageStorageKey: { } storageKey, ImageWidth: int width, ImageHeight: int height } => ImageOf(
                        new WorkImage(storageKey, ImageOriginalFileName, width, height, ImageBlurDataUri)
                    ),
                    _ => null,
                }
            );

        private SlideImage ImageOf(WorkImage image) =>
            LibraryAltText is { } altText
                ? new SlideImage.OfLibrary(image, altText, UploadedImage.NameOf(altText, Unwrap.Value(image.OriginalFileName)))
                : new SlideImage.OfWork(image, new WorkName(Unwrap.Value(WorkName)), new WorkSlug(Unwrap.Value(WorkSlug)),
                    Unwrap.Value(ImagePlace), Unwrap.Value(IsMainImage));
    }
}
