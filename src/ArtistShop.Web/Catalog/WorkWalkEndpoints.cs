namespace ArtistShop.Web.Catalog;

using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

// The steps either side of the work a lightbox opened on, fetched as the visitor swipes
public static class WorkWalkEndpoints
{
    public static void MapWorkWalkEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/works/{slug}/walk", GetAsync).WithMetadata(new ServedOnAttribute(HostTypes.Site));

    public static string WalkUrl(string workSlug, string? collectionSlug) =>
        collectionSlug is null
            ? $"/works/{workSlug}/walk"
            : $"/works/{workSlug}/walk?{WorkPageQuery.CollectionKey}={collectionSlug}";

    // the same work and walk the page at that address shows
    private static async Task<Results<Ok<WorkWalkStep>, NotFound>> GetAsync(
        string slug,
        [FromQuery(Name = WorkPageQuery.CollectionKey)] string? collectionValue,
        WorkRepository works
    )
    {
        if (await works.GetBySlugAsync(slug) is not { } work)
        {
            return TypedResults.NotFound();
        }

        var walk = await WorkWalk.LoadAsync(works, work, collectionValue);

        return TypedResults.Ok(WorkWalkStep.For(work, walk));
    }
}
