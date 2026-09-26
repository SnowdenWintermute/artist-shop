using System.Globalization;
using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Pages.Admin.Publishing.Posts.ArtworkPicker;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Components;

// The one place each page's address is spelled, other than its own @page line. The artwork page
// has query parameters too, so it has its own, ArtworkPageQuery
public static class PageUrls
{
    public static string Series(SeriesSlug slug) => $"/series/{slug.Value}";

    public const string ArtworkList = "/admin/catalog/artworks";

    public static string EditArtwork(ArtworkId id) => $"/admin/catalog/artworks/{id.Value}/edit";

    // on a site's host
    public const string AdminDashboard = "/admin";

    public const string PostList = "/admin/posts";

    // on the platform's host
    public const string Operator = "/operator";

    // on the platform's host
    public const string SignUp = "/signup";

    // on the platform's host
    public const string MySites = "/sites";

    // on the platform's host, for the site's owner
    public static string DeleteSite(SiteId id) => $"/sites/{id.Value}/delete";

    // on a site's host, for its owner
    public const string SiteAdmins = "/admin/admins";

    // A site's home page and its admin, on the platform page's scheme and port (5176 in dev). Being
    // signed in on the platform doesn't sign anyone in there: each host has its own cookie, so the
    // admin sends someone not signed in on that site to its sign-in first
    public static string SiteHome(string platformBaseUri, HostName siteHost) => OnHost(platformBaseUri, siteHost, "/");

    public static string SiteAdmin(string platformBaseUri, HostName siteHost) =>
        OnHost(platformBaseUri, siteHost, AdminDashboard);

    // the platform's home from a site's page
    public static string PlatformHome(string siteBaseUri, HostName platformHost) => OnHost(siteBaseUri, platformHost, "/");

    // My websites from a site's page, such as in an invitation's email
    public static string PlatformMySites(string siteBaseUri, HostName platformHost) =>
        OnHost(siteBaseUri, platformHost, MySites);

    private static string OnHost(string baseUri, HostName host, string path) =>
        new UriBuilder(baseUri) { Host = host.Value, Path = path }.Uri.AbsoluteUri;

    // on the platform's host
    public const string Register = "/Account/Register";

    public const string RegisterConfirmation = "/Account/RegisterConfirmation";

    public const string NewPost = "/admin/posts/new";

    public static string EditPost(PostId id) => $"/admin/posts/{id.Value}/edit";

    public const string Blog = "/posts";

    public static string Post(PostSlug slug) => $"/posts/{slug.Value}";

    // the post editor's artwork picker, which runs in a frame: the list, then one artwork's
    // images, then the choice of the image picked. The trail rides along in each address
    public const string ArtworkPicker = "/admin/posts/pick-artwork";

    public const string ArtworkPickerImageKey = "image";

    // The list's own query string holds the mode already, if it was opened in one. The trail's list
    // query only ever follows the picker's own path, so it can't send the frame anywhere else
    // With no list query, the picker's starting list: only artworks with images, since those are
    // all it can embed. The filter shows that, and the artist can switch it off
    public static string ArtworkPickerList(ArtworkPickerTrail trail) =>
        trail.ListQuery is { Length: > 0 } listQuery ? $"{ArtworkPicker}?{listQuery}"
        : WithQuery(
            ArtworkPicker,
            (ArtworkListQuery.ImagesKey, YesNoSelect.Yes),
            (ArtworkPickerTrail.ModeKey, trail.ModeValue)
        );

    public static string ArtworkPickerImages(ArtworkId id, ArtworkPickerTrail trail) =>
        ArtworkPickerImages(id.Value.ToString(CultureInfo.InvariantCulture), trail);

    // with a placeholder for a script to put an artwork's id in
    public static string ArtworkPickerImages(string artworkId, ArtworkPickerTrail trail) =>
        WithQuery(
            $"{ArtworkPicker}/{artworkId}",
            (ArtworkPickerTrail.ModeKey, trail.ModeValue),
            (ArtworkPickerTrail.ListKey, trail.ListQuery)
        );

    public static string ArtworkPickerChoice(ArtworkId id, string storageKey, ArtworkPickerTrail trail) =>
        WithQuery(
            $"{ArtworkPicker}/{id.Value}",
            (ArtworkPickerImageKey, storageKey),
            (ArtworkPickerTrail.ModeKey, trail.ModeValue),
            (ArtworkPickerTrail.ListKey, trail.ListQuery)
        );

    // leaves out a parameter with no value, so the default case has a plain address
    private static string WithQuery(string path, params (string Key, string? Value)[] parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
                .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value!)}")
        );

        return query.Length == 0 ? path : $"{path}?{query}";
    }
}
