namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

// The walk Previous and Next take from a work: the collection the visitor is going through, the
// places either side, and where the work's images sit among all of them. Loaded here for both
// the work page and the lightbox's walk endpoint, so the two never disagree. Collection is null when
// the work is in none, and then the walk is its own images
public record WorkWalk(Collection? Collection, WorkNeighbours Neighbours, WorkWalkPosition Position)
{
    public static async Task<WorkWalk> LoadAsync(
        WorkRepository works,
        Work work,
        string? collectionValue
    )
    {
        if (CollectionFor(work, collectionValue) is not { } collection)
        {
            return new(null, new WorkNeighbours(null, null), new WorkWalkPosition(0, work.Images.Count));
        }

        // a visitor is only sent on to work there is something to look at, which is the rule the
        // collection page and the home page count by
        var neighbours = await works.GetNeighboursAsync(collection.Id, work.Id, onlyWorksWithImages: true);
        var position = await works.GetWalkPositionAsync(collection.Id, work.Id);

        return new(collection, neighbours, position);
    }

    // the collection the visitor is walking through, when the work really is in it. Arriving cold,
    // or on a link to a collection it has since left, still gets arrows: the first of its own, by name
    private static Collection? CollectionFor(Work work, string? collectionValue) =>
        work.Collections.FirstOrDefault(collection => collection.Slug.Value == collectionValue)
        ?? CollectionOrder.SortedByName(work.Collections).FirstOrDefault();
}
