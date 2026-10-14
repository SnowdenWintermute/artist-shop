namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using Dapper;

// the cards a grid shows when they mix collections and works; one kind alone comes from its own repository
public class CardGridRepository(SiteDatabase database)
{
    private const short CollectionCardType = 1;

    public async Task<CardGridPage> GetCollectionsAndLooseWorksPageAsync(int pageNumber, int pageSize)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = (
            await connection.QueryAsync<CardRow>(
                "SELECT * FROM get_collections_and_loose_works_page(@LooseWorkSort, @Offset, @PageSize)",
                new
                {
                    LooseWorkSort = (short)WorkListSort.RecentlyAdded,
                    Offset = (pageNumber - 1) * pageSize,
                    PageSize = pageSize,
                }
            )
        ).ToList();

        // the count rides on the rows, so an empty page carries none
        var totalCount = rows.Count is 0 ? 0 : rows[0].TotalCount;

        return new CardGridPage([.. rows.Select(row => row.ToCard())], totalCount, pageNumber, pageSize);
    }

    private sealed class CardRow
    {
        public required short CardType { get; init; }
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public int? WorkCount { get; init; }
        public string? ImageStorageKey { get; init; }
        public string? ImageOriginalFileName { get; init; }
        public int? ImageWidth { get; init; }
        public int? ImageHeight { get; init; }
        public string? ImageBlurDataUri { get; init; }
        public required int TotalCount { get; init; }

        public GridCard ToCard()
        {
            var image = this is { ImageStorageKey: string storageKey, ImageWidth: int width, ImageHeight: int height }
                ? new WorkImage(storageKey, ImageOriginalFileName, width, height, ImageBlurDataUri)
                : null;

            return CardType == CollectionCardType
                ? new GridCard.OfCollection(
                    new CollectionWithCover(
                        new CollectionId(Id),
                        new CollectionName(Name),
                        new CollectionSlug(Slug),
                        WorkCount ?? throw new InvalidOperationException("A collection card has no work count."),
                        image
                    )
                )
                : new GridCard.OfWork(new WorkId(Id), new WorkName(Name), new WorkSlug(Slug), image);
        }
    }
}
