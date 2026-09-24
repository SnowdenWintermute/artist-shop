namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Posts;

using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

// Reads a pasted YouTube or Vimeo link for VideoAddressDialog, so links are read by the same tested
// code that checks a stored embed
public static class VideoLinkEndpoints
{
    public const string Path = "/admin/video-link";

    public static void MapVideoLinkEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Path, Read).RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));

    // the parts the embed stores, or 404 when the link isn't to a video this can read
    private static Results<Ok<VideoParts>, NotFound> Read(string? link) =>
        link is not null && VideoSources.FromLink(link) is { } source
            ? TypedResults.Ok(VideoSources.ToParts(source))
            : TypedResults.NotFound();
}
