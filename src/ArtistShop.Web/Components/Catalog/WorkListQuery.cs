namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Lists;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

// The query string holds the page's whole state, so every filtered view is a link that can be
// bookmarked or sent. This turns the raw values into a filter and names them back for the form
public static class WorkListQuery
{
    public const string TypeKey = "type";
    public const string TermKey = "term";
    public const string CollectionKey = "collection";
    // the collection filter's value for works in no collection, where any other value is a collection id
    public const string NoCollectionValue = "none";
    public const string SearchKey = "q";
    public const string ImagesKey = "images";
    public const string SaleKey = "sale";
    public const string SortKey = "sort";

    public static WorkListFilter Read(
        int[]? typeIds,
        int[]? termIds,
        string? collection,
        string? search,
        string? images,
        string? sale,
        string? sort,
        string? page
    )
    {
        WorkCollectionFilter? collectionFilter =
            collection == NoCollectionValue ? new WorkCollectionFilter.InNoCollection()
            : int.TryParse(collection, out var chosenCollection) ? new WorkCollectionFilter.InCollection(new CollectionId(chosenCollection))
            : null;

        return new(
            [.. (typeIds ?? []).Select(id => new WorkTypeId(id))],
            [.. (termIds ?? []).Select(id => new VocabularyTermId(id))],
            collectionFilter,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            YesNoSelect.Read(images),
            YesNoSelect.Read(sale),
            ReadSort(sort, collectionFilter),
            PageLinks.ReadPageNumber(page)
        );
    }

    // the filter a link to the list shows, such as its Clear filters link
    public static WorkListFilter ReadAddress(string address)
    {
        var queryStart = address.IndexOf('?');
        var query = QueryHelpers.ParseQuery(queryStart < 0 ? null : address[queryStart..]);

        return Read(
            typeIds: Ids(query.GetValueOrDefault(TypeKey)),
            termIds: Ids(query.GetValueOrDefault(TermKey)),
            collection: query.GetValueOrDefault(CollectionKey),
            search: query.GetValueOrDefault(SearchKey),
            images: query.GetValueOrDefault(ImagesKey),
            sale: query.GetValueOrDefault(SaleKey),
            sort: query.GetValueOrDefault(SortKey),
            page: query.GetValueOrDefault(PageLinks.PageKey)
        );
    }

    private static int[] Ids(StringValues values) =>
        [.. values.Select(value => int.TryParse(value, out var id) ? id : (int?)null).OfType<int>()];

    private static WorkListSort ReadSort(string? value, WorkCollectionFilter? collectionFilter) =>
        value switch
        {
            "title" => WorkListSort.TitleAscending,
            "title-desc" => WorkListSort.TitleDescending,
            "newest" => WorkListSort.DateCreatedNewest,
            "oldest" => WorkListSort.DateCreatedOldest,
            // the filter bar only offers this with a collection picked, and without one it sorts by
            // nothing: a link carrying it alone would leave the control reading "Recently added"
            "collection" when collectionFilter is WorkCollectionFilter.InCollection => WorkListSort.CollectionOrder,
            _ => WorkListSort.RecentlyAdded,
        };

    public static string Value(WorkListSort sort) =>
        sort switch
        {
            WorkListSort.TitleAscending => "title",
            WorkListSort.TitleDescending => "title-desc",
            WorkListSort.DateCreatedNewest => "newest",
            WorkListSort.DateCreatedOldest => "oldest",
            WorkListSort.CollectionOrder => "collection",
            WorkListSort.RecentlyAdded => "added",
        };

    public static string Label(WorkListSort sort, SiteWording wording) =>
        sort switch
        {
            WorkListSort.TitleAscending => "Title A to Z",
            WorkListSort.TitleDescending => "Title Z to A",
            WorkListSort.DateCreatedNewest => $"Newest {wording.Work.SingularInSentence}",
            WorkListSort.DateCreatedOldest => $"Oldest {wording.Work.SingularInSentence}",
            WorkListSort.CollectionOrder => $"The order in this {wording.Collection.SingularInSentence}",
            WorkListSort.RecentlyAdded => "Recently added",
        };
}
