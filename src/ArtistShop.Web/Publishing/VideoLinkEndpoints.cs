namespace ArtistShop.Web.Publishing;

using System.Text.Json.Serialization;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;

// A video embed's value in the Delta, less its layout: what the editor stores, and what the
// endpoint hands it. Hash is left out rather than null when there is none, as the editor stores it
public record VideoParts(
    string Provider,
    string VideoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hash
);

// Reads a pasted YouTube or Vimeo link for VideoAddressDialog, so links are read by the same tested
// code that checks a stored embed
public static class VideoLinkEndpoints
{
    public const string Path = "/admin/video-link";

    public static void MapVideoLinkEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(Path, Read)
            .RequireAuthorization(SitePolicies.Admin)
            .WithMetadata(new ServedOnAttribute(HostTypes.Site));

    public static VideoParts ToParts(VideoSource source) =>
        source switch
        {
            YouTubeVideo video => new VideoParts(VideoSources.YouTubeProvider, video.Id, null),
            VimeoVideo video => new VideoParts(VideoSources.VimeoProvider, video.Id, video.UnlistedHash),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    // The parts the embed stores, or 422 when the link isn't to a video this can read. Not 404,
    // which a missing route answers too
    private static Results<Ok<VideoParts>, UnprocessableEntity> Read(string? link) =>
        link is not null && VideoSources.FromLink(link) is { } source
            ? TypedResults.Ok(ToParts(source))
            : TypedResults.UnprocessableEntity();
}
