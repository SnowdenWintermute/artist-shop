namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;
using Dapper;
using Npgsql;

public class CustomPageRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_custom_pages_name";
    private const string UniqueAddressConstraint = "unique_custom_pages_address";

    public async Task<List<CustomPage>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CustomPageRow>("SELECT * FROM get_all_custom_pages()");

        return [.. rows.Select(row => row.ToPage())];
    }

    public async Task<CustomPage?> GetAsync(CustomPageId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<CustomPageRow>(
            "SELECT * FROM get_custom_page(@Id)",
            new { Id = id.Value }
        );

        return row?.ToPage();
    }

    // an empty page in no nav
    public async Task<CustomPageId> AddAsync(string name, CustomPageAddress address)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            return new CustomPageId(
                await connection.QuerySingleAsync<int>(
                    "SELECT add_custom_page(@Name, @Address)",
                    new { Name = name, Address = address.Value }
                )
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueAddressConstraint))
        {
            throw new AddressAlreadyInUseException(address.Value);
        }
    }

    public async Task UpdateAsync(CustomPageId id, string name, CustomPageAddress address)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT update_custom_page(@Id, @Name, @Address)",
                new
                {
                    Id = id.Value,
                    Name = name,
                    Address = address.Value,
                }
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name);
        }
        catch (PostgresException exception) when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueAddressConstraint))
        {
            throw new AddressAlreadyInUseException(address.Value);
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CustomPageNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the home and blog pages are refused, since / always shows one and posts always link up to the other
    public async Task DeleteAsync(CustomPageId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync("SELECT delete_custom_page(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CustomPageIsHome) || SqlErrors.IsThrown(exception, SqlStates.CustomPageIsBlog))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task MakeBlogAsync(CustomPageId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync("SELECT make_custom_page_blog(@Id)", new { Id = id.Value });
        }
        // the page lost its post list meanwhile, as the admin only offers this on pages holding one
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CustomPageNoLongerExists)
                || SqlErrors.IsThrown(exception, SqlStates.BlogPageNeedsPostList))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    public async Task MakeHomeAsync(CustomPageId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync("SELECT make_custom_page_home(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.CustomPageNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // a website always has one: the first migration makes it, and it can't be deleted while it's home
    public async Task<CustomPage> GetHomeAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<CustomPageRow>("SELECT * FROM get_home_custom_page()");

        return row.ToPage();
    }

    // a website always has one: the first migration makes it, and it can't be deleted while it's the blog page
    public async Task<CustomPage> GetBlogAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<CustomPageRow>("SELECT * FROM get_blog_custom_page()");

        return row.ToPage();
    }

    public async Task<HashSet<CustomPageId>> GetIdsHoldingAsync(CustomComponentType type)
    {
        await using var connection = await database.OpenConnectionAsync();

        var ids = await connection.QueryAsync<int>("SELECT * FROM get_custom_page_ids_holding(@Type)", new { Type = (int)type });

        return [.. ids.Select(id => new CustomPageId(id))];
    }

    public async Task<CustomPage?> GetByAddressAsync(CustomPageAddress address)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<CustomPageRow>(
            "SELECT * FROM get_custom_page_by_address(@Address)",
            new { Address = address.Value }
        );

        return row?.ToPage();
    }

    // the pages with a text component on them that embeds the work
    public async Task<List<CustomPage>> GetMentioningWorkAsync(WorkId workId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CustomPageRow>(
            "SELECT * FROM get_custom_pages_mentioning_work(@WorkId)",
            new { WorkId = workId.Value }
        );

        return [.. rows.Select(row => row.ToPage())];
    }

    // empty when the page has no rows, or no longer exists
    public async Task<CustomPageLayout> GetLayoutAsync(CustomPageId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        var placements = await connection.QueryAsync<PlacementRow>(
            "SELECT * FROM get_custom_page_layout(@Id)",
            new { Id = id.Value }
        );

        // GroupBy keeps the order the rows arrive in
        var rows = new List<PageRow>();

        foreach (var row in placements.GroupBy(placement => placement.RowId))
        {
            var first = row.First();
            var slots = row
                .Where(placement => placement.SlotId is not null)
                .GroupBy(placement => Unwrap.Value(placement.SlotId))
                .Select(slot => new RowSlot(
                    new RowSlotId(slot.Key),
                    [.. slot.Where(placement => placement.ComponentId is not null).Select(placement => placement.ToComponent())]
                ));

            var padding = new RowPadding(first.TopPadding, first.BottomPadding, first.SidePadding);
            rows.Add(new PageRow(new PageRowId(row.Key), first.Widths, first.Alignment, first.Height, first.ContentWidth, padding, first.Gap, first.ToTheme(), first.ToBackground(), [.. slots]));
        }

        return new CustomPageLayout(rows);
    }

    // shared with NavEntryRepository, whose function returns the same columns
    internal sealed class CustomPageRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Address { get; init; }
        public required bool IsHome { get; init; }
        public required bool IsBlog { get; init; }

        public CustomPage ToPage() => new(new CustomPageId(Id), Name, new CustomPageAddress(Address), IsHome, IsBlog);
    }

    private sealed class PlacementRow
    {
        public required int RowId { get; init; }
        public required RowWidths Widths { get; init; }
        public required RowAlignment Alignment { get; init; }
        public required RowHeight Height { get; init; }
        public required RowContentWidth ContentWidth { get; init; }
        public required RowVerticalPadding TopPadding { get; init; }
        public required RowVerticalPadding BottomPadding { get; init; }
        public required RowSidePadding SidePadding { get; init; }
        public required RowGap Gap { get; init; }
        public required ThemePreset? ThemePreset { get; init; }
        public required int? ThemeId { get; init; }
        public required string? ThemeName { get; init; }
        public required string? ThemeSettings { get; init; }
        public required RowBackgroundType BackgroundType { get; init; }
        public required string? BackgroundColor { get; init; }
        public required string? BackgroundUploadedImageKey { get; init; }
        public required int? BackgroundWorkId { get; init; }
        public required string? BackgroundWorkImageKey { get; init; }
        public required ImageFit? BackgroundFit { get; init; }
        public required int? SlotId { get; init; }
        public required int? ComponentId { get; init; }
        public required CustomComponentType? ComponentType { get; init; }
        public required string? ComponentName { get; init; }
        public required string? Settings { get; init; }
        public required int? SettingsVersion { get; init; }

        public RowBackground ToBackground() =>
            BackgroundType switch
            {
                RowBackgroundType.None => new RowBackground.None(),
                RowBackgroundType.Color => new RowBackground.Color(RgbColor.Parse(Unwrap.Value(BackgroundColor))),
                RowBackgroundType.Image => new RowBackground.Image(
                    BackgroundWorkId is int workId ? new ImageSource.Work(new WorkId(workId), BackgroundWorkImageKey)
                    : BackgroundUploadedImageKey is { } storageKey ? new ImageSource.Library(storageKey)
                    : null,
                    Unwrap.Value(BackgroundFit),
                    BackgroundColor is { } behind ? RgbColor.Parse(behind) : null
                ),
                _ => throw new InvalidOperationException($"{BackgroundType} isn't a background."),
            };

        public NamedTheme? ToTheme() => ThemeRepository.NamedThemeOf(ThemeId, ThemeName, ThemeSettings, ThemePreset);

        public CustomComponent ToComponent() =>
            new(
                new CustomComponentId(Unwrap.Value(ComponentId)),
                Unwrap.Value(ComponentName),
                CustomComponentSettingsJson.Read(Unwrap.Value(ComponentType), Unwrap.Value(SettingsVersion), Unwrap.Value(Settings))
            );
    }
}
