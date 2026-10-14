namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Domain.Catalog;

// one card in a grid; each kind is drawn by its own component, so their looks can differ
public abstract record GridCard
{
    public sealed record OfCollection(CollectionWithCover Collection) : GridCard;

    public sealed record OfWork(WorkId Id, WorkName Name, WorkSlug Slug, WorkImage? Image) : GridCard;
}

public record CardGridPage(IReadOnlyList<GridCard> Cards, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
