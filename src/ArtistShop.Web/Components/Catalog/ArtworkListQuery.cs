namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Catalog;

// The query string holds the page's whole state, so every filtered view is a link that can be
// bookmarked or sent. This turns the raw values into a filter and names them back for the form
public static class ArtworkListQuery
{
    public const string TypeKey = "type";
    public const string TermKey = "term";
    public const string SeriesKey = "series";
    public const string SearchKey = "q";
    public const string ImagesKey = "images";
    public const string SaleKey = "sale";
    public const string SortKey = "sort";
    public const string PageKey = "page";

    public static ArtworkListFilter Read(
        int[]? typeIds,
        int[]? termIds,
        string? series,
        string? search,
        string? images,
        string? sale,
        string? sort,
        string? page
    ) =>
        new(
            [.. (typeIds ?? []).Select(id => new ArtworkTypeId(id))],
            [.. (termIds ?? []).Select(id => new VocabularyTermId(id))],
            int.TryParse(series, out var seriesId) ? new SeriesId(seriesId) : null,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            YesNoSelect.Read(images),
            YesNoSelect.Read(sale),
            ReadSort(sort),
            int.TryParse(page, out var pageNumber) && pageNumber > 1 ? pageNumber : 1
        );

    private static ArtworkListSort ReadSort(string? value) =>
        value switch
        {
            "title" => ArtworkListSort.TitleAscending,
            "title-desc" => ArtworkListSort.TitleDescending,
            "newest" => ArtworkListSort.DateCreatedNewest,
            "oldest" => ArtworkListSort.DateCreatedOldest,
            "series" => ArtworkListSort.SeriesOrder,
            _ => ArtworkListSort.RecentlyAdded,
        };

    public static string Value(ArtworkListSort sort) =>
        sort switch
        {
            ArtworkListSort.TitleAscending => "title",
            ArtworkListSort.TitleDescending => "title-desc",
            ArtworkListSort.DateCreatedNewest => "newest",
            ArtworkListSort.DateCreatedOldest => "oldest",
            ArtworkListSort.SeriesOrder => "series",
            ArtworkListSort.RecentlyAdded => "added",
        };

    public static string Label(ArtworkListSort sort) =>
        sort switch
        {
            ArtworkListSort.TitleAscending => "Title A to Z",
            ArtworkListSort.TitleDescending => "Title Z to A",
            ArtworkListSort.DateCreatedNewest => "Newest work",
            ArtworkListSort.DateCreatedOldest => "Oldest work",
            ArtworkListSort.SeriesOrder => "The order in this series",
            ArtworkListSort.RecentlyAdded => "Recently added",
        };
}
