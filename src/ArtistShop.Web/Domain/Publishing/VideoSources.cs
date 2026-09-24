namespace ArtistShop.Web.Domain.Publishing;

using System.Collections.Specialized;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

// A video embed's value in the Delta, less its layout: what the editor stores, and what the
// video link endpoint hands it. Hash is left out rather than null when there is none, as the
// editor stores it
public record VideoParts(
    string Provider,
    string VideoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hash
);

// Which video a stored embed or a pasted link names. Every part becomes part of a player's
// address, so each must have its site's exact shape
public static partial class VideoSources
{
    private const string YouTubeProvider = "youtube";
    private const string VimeoProvider = "vimeo";

    // the pages a YouTube video is watched from, besides watch?v=, whose next part is the id
    private static readonly string[] YouTubeIdPaths = ["shorts", "embed", "live", "v"];

    // A malformed hash drops the video rather than the hash, since Vimeo won't play it without one
    public static VideoSource? FromParts(string? provider, string? videoId, string? hash) =>
        (provider, videoId, hash) switch
        {
            (YouTubeProvider, { } id, _) when YouTubeVideoIdPattern().IsMatch(id) => new YouTubeVideo(id),
            (VimeoProvider, { } id, null) when VimeoVideoIdPattern().IsMatch(id) => new VimeoVideo(id, null),
            (VimeoProvider, { } id, { } unlistedHash)
                when VimeoVideoIdPattern().IsMatch(id) && VimeoHashPattern().IsMatch(unlistedHash) =>
                new VimeoVideo(id, unlistedHash),
            _ => null,
        };

    public static VideoParts ToParts(VideoSource source) =>
        source switch
        {
            YouTubeVideo video => new VideoParts(YouTubeProvider, video.Id, null),
            VimeoVideo video => new VideoParts(VimeoProvider, video.Id, video.UnlistedHash),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    // Which video a link is to, or null if it isn't one this can read. A link pasted without its
    // https:// is read as if it had it
    public static VideoSource? FromLink(string link)
    {
        var trimmed = link.Trim();
        var withScheme = HasSchemePattern().IsMatch(trimmed) ? trimmed : $"https://{trimmed}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            return null;
        }

        var host = MobileOrMusicSubdomainPattern().Replace(uri.Host, "");
        var path = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var query = HttpUtility.ParseQueryString(uri.Query);

        return FromYouTubeLink(host, path, query) ?? FromVimeoLink(host, path, query);
    }

    // youtube.com/watch?v=…, youtu.be/…, and the shorts, embed and live addresses
    private static VideoSource? FromYouTubeLink(string host, string[] path, NameValueCollection query)
    {
        var id = host switch
        {
            "youtu.be" => path.ElementAtOrDefault(0),
            "youtube.com" or "youtube-nocookie.com" => path.ElementAtOrDefault(0) switch
            {
                "watch" => query["v"],
                { } page when YouTubeIdPaths.Contains(page) => path.ElementAtOrDefault(1),
                _ => null,
            },
            _ => null,
        };

        return FromParts(YouTubeProvider, id, null);
    }

    // vimeo.com/…/{id}, where an unlisted video's link has its hash after the id, and the player's
    // own player.vimeo.com/video/{id}?h={hash}
    private static VideoSource? FromVimeoLink(string host, string[] path, NameValueCollection query)
    {
        if (host is not ("vimeo.com" or "player.vimeo.com"))
        {
            return null;
        }

        // A video's own link starts with its id, and a hash after it can be all digits too. A
        // channel's or showcase's link has the video's id last, with the showcase's own number earlier
        var idIndex =
            path.Length > 0 && VimeoVideoIdPattern().IsMatch(path[0])
                ? 0
                : Array.FindLastIndex(path, part => VimeoVideoIdPattern().IsMatch(part));

        return idIndex < 0
            ? null
            : FromParts(VimeoProvider, path[idIndex], path.ElementAtOrDefault(idIndex + 1) ?? query["h"]);
    }

    // \z is the very end: $ would also match before a final "\n", letting one through.
    // Every YouTube video id is eleven of these characters
    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}\z")]
    private static partial Regex YouTubeVideoIdPattern();

    [GeneratedRegex(@"^[0-9]{1,12}\z")]
    private static partial Regex VimeoVideoIdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,32}\z")]
    private static partial Regex VimeoHashPattern();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex HasSchemePattern();

    [GeneratedRegex(@"^(www|m|music)\.")]
    private static partial Regex MobileOrMusicSubdomainPattern();
}
