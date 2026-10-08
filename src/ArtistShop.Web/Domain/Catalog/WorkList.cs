namespace ArtistShop.Web.Domain.Catalog;

// the numbers travel to get_work_list, which orders by them
public enum WorkListSort : byte
{
    RecentlyAdded = 1,
    TitleAscending = 2,
    TitleDescending = 3,
    DateCreatedNewest = 4,
    DateCreatedOldest = 5,

    // only means anything with a collection chosen: it is the artist's order inside that collection
    CollectionOrder = 6,
}

// which collections a listed work is in; a null filter keeps works in any collection or none
public abstract record WorkCollectionFilter
{
    // only the two below
    private WorkCollectionFilter() { }

    public sealed record InCollection(CollectionId CollectionId) : WorkCollectionFilter;

    public sealed record InNoCollection : WorkCollectionFilter;
}

// a null for a three-state filter means the filter is off: HasImages null keeps both
public record WorkListFilter(
    IReadOnlyList<WorkTypeId> WorkTypeIds,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    WorkCollectionFilter? Collection,
    string? SearchText,
    bool? HasImages,
    bool? IsForSale,
    WorkListSort Sort,
    int PageNumber
)
{
    // the sort and the page number are not filters: neither of them hides a work
    public bool HasSameFiltersAs(WorkListFilter other) =>
        WorkTypeIds.ToHashSet().SetEquals(other.WorkTypeIds)
        && VocabularyTermIds.ToHashSet().SetEquals(other.VocabularyTermIds)
        && Collection == other.Collection
        && SearchText == other.SearchText
        && HasImages == other.HasImages
        && IsForSale == other.IsForSale;
}

public record WorkListItem(
    WorkId Id,
    WorkName Name,
    WorkSlug Slug,
    WorkTypeName WorkTypeName,
    PartialDate? DateCreated,
    string? CollectionNames,
    int ImageCount,
    bool IsForSale,
    WorkImage? PrimaryImage
);

public record WorkListPage(
    IReadOnlyList<WorkListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
