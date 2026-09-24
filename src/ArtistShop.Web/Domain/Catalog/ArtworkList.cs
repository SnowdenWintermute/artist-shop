namespace ArtistShop.Web.Domain.Catalog;

// the numbers travel to get_artwork_list, which orders by them
public enum ArtworkListSort : byte
{
    RecentlyAdded = 1,
    TitleAscending = 2,
    TitleDescending = 3,
    DateCreatedNewest = 4,
    DateCreatedOldest = 5,

    // only means anything with a series chosen: it is the artist's order inside that series
    SeriesOrder = 6,
}

// a null for a three-state filter means the filter is off: HasImages null keeps both
public record ArtworkListFilter(
    IReadOnlyList<ArtworkTypeId> ArtworkTypeIds,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    SeriesId? SeriesId,
    string? SearchText,
    bool? HasImages,
    bool? IsForSale,
    ArtworkListSort Sort,
    int PageNumber
)
{
    // the sort and the page number are not filters: neither of them hides an artwork
    public bool HasSameFiltersAs(ArtworkListFilter other) =>
        ArtworkTypeIds.ToHashSet().SetEquals(other.ArtworkTypeIds)
        && VocabularyTermIds.ToHashSet().SetEquals(other.VocabularyTermIds)
        && SeriesId == other.SeriesId
        && SearchText == other.SearchText
        && HasImages == other.HasImages
        && IsForSale == other.IsForSale;
}

public record ArtworkListItem(
    ArtworkId Id,
    ArtworkName Name,
    ArtworkSlug Slug,
    ArtworkTypeName ArtworkTypeName,
    PartialDate? DateCreated,
    string? SeriesNames,
    int ImageCount,
    bool IsForSale,
    ArtworkImage? PrimaryImage
);

public record ArtworkListPage(
    IReadOnlyList<ArtworkListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
