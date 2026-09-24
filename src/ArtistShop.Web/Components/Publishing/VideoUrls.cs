namespace ArtistShop.Web.Components.Publishing;

using ArtistShop.Web.Domain.Publishing;

// The one place a video's addresses are built, for the post page and the editor alike: the player
// the page embeds, and the video's own page, which the editor shows as the embed's link. Each
// player is its site's privacy-minded one: youtube-nocookie sets no cookies until the video plays,
// and Vimeo's dnt stops it tracking the visitor
public static class VideoUrls
{
    public static string Player(VideoSource source) =>
        source switch
        {
            YouTubeVideo video => YouTubePlayer(video.Id),
            VimeoVideo video => VimeoPlayer(video.Id, video.UnlistedHash),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    public static string YouTubePlayer(string id) => $"https://www.youtube-nocookie.com/embed/{id}";

    public static string VimeoPlayer(string id, string? unlistedHash) =>
        $"https://player.vimeo.com/video/{id}?dnt=1" + (unlistedHash is null ? "" : $"&h={unlistedHash}");

    public static string YouTubePage(string id) => $"https://www.youtube.com/watch?v={id}";

    public static string VimeoPage(string id, string? unlistedHash) =>
        $"https://vimeo.com/{id}" + (unlistedHash is null ? "" : $"/{unlistedHash}");
}
