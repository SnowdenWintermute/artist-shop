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
    )
    {
        SeriesId? seriesId = int.TryParse(series, out var chosenSeries)
            ? new SeriesId(chosenSeries)
            : null;

        return new(
            [.. (typeIds ?? []).Select(id => new ArtworkTypeId(id))],
            [.. (termIds ?? []).Select(id => new VocabularyTermId(id))],
            seriesId,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            YesNoSelect.Read(images),
            YesNoSelect.Read(sale),
            ReadSort(sort, seriesId),
            ReadPage(page)
        );
    }

    // anything below the first page is the first page, so a hand-edited link lands somewhere real
    public static int ReadPage(string? value) =>
        int.TryParse(value, out var pageNumber) && pageNumber > 1 ? pageNumber : 1;

    private static ArtworkListSort ReadSort(string? value, SeriesId? seriesId) =>
        value switch
        {
            "title" => ArtworkListSort.TitleAscending,
            "title-desc" => ArtworkListSort.TitleDescending,
            "newest" => ArtworkListSort.DateCreatedNewest,
            "oldest" => ArtworkListSort.DateCreatedOldest,
            // the filter bar only offers this with a series picked, and without one it sorts by
            // nothing: a link carrying it alone would leave the control reading "Recently added"
            "series" when seriesId is not null => ArtworkListSort.SeriesOrder,
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
