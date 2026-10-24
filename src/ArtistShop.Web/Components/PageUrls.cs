using System.Globalization;
using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Components.Pages.Admin.Catalog.WorkPicker;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Domain.Website;
using Microsoft.AspNetCore.WebUtilities;

namespace ArtistShop.Web.Components;

// The one place each page's address is spelled, other than its own @page line. The work page
// has query parameters too, so it has its own, WorkPageQuery
public static class PageUrls
{
    public static string Collection(CollectionSlug slug) => $"/collections/{slug.Value}";

    public const string WorkList = "/admin/catalog/works";

    public static string WorkListOfType(WorkTypeId typeId) =>
        WithQuery(WorkList, (WorkListQuery.TypeKey, typeId.Value.ToString(CultureInfo.InvariantCulture)));
    // with no type, the one its admin last chose, else the page asks for one
    public static string WorkBulkImageUpload(WorkTypeId? typeId) =>
        WithQuery(
            "/admin/catalog/works/images",
            ("type", typeId?.Value.ToString(CultureInfo.InvariantCulture))
        );

    // ?type= picks the type; with none, the page goes to the type its admin last chose
    public const string AddWork = "/admin/catalog/works/add";

    // with no type, the one its admin last chose, else the page asks for one
    public const string WorkImport = "/admin/catalog/works/import";

    // ?type= picks the type to start with; with none, the one its admin last chose, else the page asks
    public const string AddWorksFromImages = "/admin/catalog/works/add-from-images";

    // takes the work list's filters, with one type; with none, the type its admin last chose, else the first by name
    public const string WorkTable = "/admin/catalog/works/table";

    public const string NewWorkType = "/admin/catalog/types/new";

    public const string NewWorkTypeAddedQueryKey = "added";

    public static string NewWorkTypeAdded(WorkTypeId id) => $"{NewWorkType}?{NewWorkTypeAddedQueryKey}={id.Value}";

    public static string EditWorkType(WorkTypeId id) => $"/admin/catalog/types/{id.Value}/edit";

    public const string WorkTypeSettings = "/admin/catalog/types/settings";

    public const string WorkTypeImport = "/admin/catalog/types/import";

    // where the admin's Vocabularies links land, as it lists the vocabularies not linked to a type
    public const string NewVocabulary = "/admin/catalog/vocabularies/new";

    public const string NewVocabularyAddedQueryKey = "added";

    public static string NewVocabularyAdded(VocabularyId id) => $"{NewVocabulary}?{NewVocabularyAddedQueryKey}={id.Value}";

    public static string EditVocabulary(VocabularyId id) => $"/admin/catalog/vocabularies/{id.Value}/edit";

    public const string VocabularyImport = "/admin/catalog/vocabularies/import";

    public static string EditWork(WorkId id) => $"/admin/catalog/works/{id.Value}/edit";

    public const string CollectionList = "/admin/catalog/collections";

    public static string EditCollection(CollectionId id) => $"{CollectionList}/{id.Value}";

    // one parameter per item checked on a page that adds works or collections somewhere
    public const string AddCheckedKey = "add";

    public static string AddCollectionWorks(CollectionId id) => $"{EditCollection(id)}/add-works";

    // the page with these works checked and no filters, which is where Clear filters goes
    public static string AddCollectionWorks(CollectionId id, IReadOnlyList<WorkId> checkedIds) =>
        WithQuery(
            AddCollectionWorks(id),
            [
                .. checkedIds.Select(checkedId =>
                    (AddCheckedKey, (string?)checkedId.Value.ToString(CultureInfo.InvariantCulture))
                ),
            ]
        );

    // on a site's host
    public const string Home = "/";

    // on a site's host: the home page is at / alone
    public static string CustomPage(CustomPage page) => page.IsHome ? Home : $"/{page.Address.Value}";

    public const string AdminDashboard = "/admin";

    public const string PostList = "/admin/posts";

    public const string Images = "/admin/images";

    // on the platform's host
    public const string Operator = "/operator";

    // on the platform's host
    public const string OperatorSites = "/operator/sites";

    // on the platform's host
    public static string OperatorDeleteSite(SiteId id) => $"{OperatorSites}/{id.Value}/delete";

    // on the platform's host
    public const string OperatorFonts = "/operator/fonts";

    // on the platform's host
    public const string SignUp = "/signup";

    // on the platform's host
    public const string MySites = "/sites";

    // on the platform's host, for the site's owner
    public static string DeleteSite(SiteId id) => $"/sites/{id.Value}/delete";

    // on a site's host, for its owner
    public const string SiteAdmins = "/admin/admins";

    // on a site's host
    public const string Export = "/admin/export";

    // on a site's host: what the website calls collections and works
    public const string Wording = "/admin/website/wording";

    // on a site's host: the website's themes, opening on the one in use
    public const string Theme = "/admin/website/theme";

    // on a site's host: the website's logos and browser tab icon
    public const string Branding = "/admin/website/branding";

    // on a site's host: the artist's pages and the nav
    public const string CustomPageList = "/admin/website/pages";

    // on a site's host: every component, on a page or not
    public const string CustomComponentList = "/admin/website/components";

    // on a site's host: the settings of the fixed page each collection has
    public const string CollectionPageSettings = "/admin/website/collection-page";

    // the collection the settings page previews
    public const string PreviewCollectionKey = "collection";

    // previewing the collection whose page this was opened from
    public static string CollectionPageSettingsFor(CollectionId id) => $"{CollectionPageSettings}?{PreviewCollectionKey}={id.Value}";

    public static string EditCustomPage(CustomPageId id) => $"{CustomPageList}/{id.Value}";

    public static string EditPageRow(CustomPageId pageId, PageRowId rowId) => $"{EditCustomPage(pageId)}/rows/{rowId.Value}";

    // not "page" and "row": the components in the page's preview read its address too, and a post list
    // takes "page" as its page of posts
    public const string ComponentPageKey = "fromPage";

    public const string ComponentRowKey = "fromRow";

    // from a row's editor, which its Up link goes back to and its preview shows
    public static string EditCustomComponent(CustomComponentId id, CustomPageId pageId, PageRowId rowId) =>
        EditCustomComponent(id, new CustomComponentOrigin(pageId, rowId));

    // origin is the row editor the settings page was opened from, if it was
    public static string EditCustomComponent(CustomComponentId id, CustomComponentOrigin? origin) =>
        WithQuery(CustomComponent(id), [.. OriginFields(origin)]);

    // A card grid's pages adding to its custom list, with these items checked and no filters; they keep the
    // settings page's origin, so they can go back to it as it was
    public static string AddCardGridWorks(CustomComponentId id, CustomComponentOrigin? origin, IReadOnlyList<WorkId> checkedIds) =>
        WithQuery($"{CustomComponent(id)}/add-works", [.. OriginFields(origin), .. CheckedFields(checkedIds.Select(checkedId => checkedId.Value))]);

    public static string AddCardGridCollections(CustomComponentId id, CustomComponentOrigin? origin, IReadOnlyList<CollectionId> checkedIds) =>
        WithQuery($"{CustomComponent(id)}/add-collections",
            [.. OriginFields(origin), .. CheckedFields(checkedIds.Select(checkedId => checkedId.Value))]);

    // the component alone in the public layout, for the settings page's preview when it has no page to show
    public static string CustomComponentPreview(CustomComponentId id) => $"{CustomComponent(id)}/preview";

    private static string CustomComponent(CustomComponentId id) => $"{CustomComponentList}/{id.Value}";

    // for a GET form on those pages to keep, as it sends only its own fields
    public static (string Key, string Value)[] OriginFields(CustomComponentOrigin? origin) =>
        origin is null
            ? []
            :
            [
                (ComponentPageKey, origin.PageId.Value.ToString(CultureInfo.InvariantCulture)),
                (ComponentRowKey, origin.RowId.Value.ToString(CultureInfo.InvariantCulture)),
            ];

    private static IEnumerable<(string Key, string? Value)> CheckedFields(IEnumerable<int> checkedIds) =>
        checkedIds.Select(checkedId => (AddCheckedKey, (string?)checkedId.ToString(CultureInfo.InvariantCulture)));

    // on a site's host: the Theme page with that theme open
    public static string ThemeEditing(ThemeKey key) => WithQuery(Theme, ("theme", key.QueryValue));

    // on a site's host: which hints show, for the signed-in account
    public const string Hints = "/admin/hints";

    // on a site's host
    public const string Import = "/admin/import";

    // on a site's host
    public const string PostImport = "/admin/import/posts";

    public const string LookImport = "/admin/import/look";

    // on a site's host
    public const string WebsiteImport = "/admin/import/website";

    // A site's home page, its admin and its Export page, on the platform page's scheme and port (5176 in dev). Being
    // signed in on the platform doesn't sign anyone in there: each host has its own cookie, so the
    // admin sends someone not signed in on that site to its sign-in first
    public static string SiteHome(string platformBaseUri, HostName siteHost) =>
        OnHost(platformBaseUri, siteHost, "/");

    public static string SiteAdmin(string platformBaseUri, HostName siteHost) =>
        OnHost(platformBaseUri, siteHost, AdminDashboard);

    public static string SiteExport(string platformBaseUri, HostName siteHost) =>
        OnHost(platformBaseUri, siteHost, Export);

    // the platform's home from a site's page
    public static string PlatformHome(string siteBaseUri, HostName platformHost) =>
        OnHost(siteBaseUri, platformHost, "/");

    // My websites from a site's page, such as in an invitation's email
    public static string PlatformMySites(string siteBaseUri, HostName platformHost) =>
        OnHost(siteBaseUri, platformHost, MySites);

    // a website's delete page from its own admin
    public static string PlatformDeleteSite(string siteBaseUri, HostName platformHost, SiteId id) =>
        OnHost(siteBaseUri, platformHost, DeleteSite(id));

    // on a website's host: where the platform sends a browser with its sign-in code (SiteSignIns)
    public const string SiteHandoff = "/Account/Handoff";

    // on the platform's host
    public const string SiteSignIn = "/Account/SignInTo";

    // the platform's step of signing in on the website at siteHost, from that website's page
    public static string PlatformSiteSignIn(
        string siteBaseUri,
        HostName platformHost,
        string siteHost,
        string nonce
    ) =>
        QueryHelpers.AddQueryString(
            OnHost(siteBaseUri, platformHost, SiteSignIn),
            new Dictionary<string, string?> { ["site"] = siteHost, ["nonce"] = nonce }
        );

    // the website's handoff, from the platform's page
    public static string SiteHandoffWithCode(
        string platformBaseUri,
        HostName siteHost,
        string code
    ) => QueryHelpers.AddQueryString(OnHost(platformBaseUri, siteHost, SiteHandoff), "code", code);

    // the account page, from a website's page
    public static string PlatformAccount(string siteBaseUri, HostName platformHost) =>
        OnHost(siteBaseUri, platformHost, AccountPage);

    public const string AccountPage = "/Account/Manage";

    private static string OnHost(string baseUri, HostName host, string path) =>
        new UriBuilder(baseUri) { Host = host.Value, Path = path }
            .Uri
            .AbsoluteUri;

    // on the platform's host
    public const string Register = "/Account/Register";

    public const string RegisterConfirmation = "/Account/RegisterConfirmation";

    // on the platform's host, from the link Register emails
    public const string ChoosePassword = "/Account/ChoosePassword";

    public const string Login = "/Account/Login";

    public const string ForgotPassword = "/Account/ForgotPassword";

    public const string ResetPassword = "/Account/ResetPassword";

    public const string NewPost = "/admin/posts/new";

    public static string EditPost(PostId id) => $"/admin/posts/{id.Value}/edit";

    public static string Post(PostSlug slug) => $"/posts/{slug.Value}";

    // the image picker's library step, in its frame
    public const string ImagePicker = "/admin/images/pick";

    // the post editor's work picker, which runs in a frame: the list, then one work's
    // images, then the choice of the image picked. The trail rides along in each address
    public const string WorkPicker = "/admin/catalog/works/pick";

    public const string WorkPickerImageKey = "image";

    // The list's own query string holds the mode already, if it was opened in one. The trail's list
    // query only ever follows the picker's own path, so it can't send the frame anywhere else
    // With no list query, the picker's starting list: only works with images, since those are
    // all it can embed. The filter shows that, and the artist can switch it off
    public static string WorkPickerList(WorkPickerTrail trail) =>
        trail.ListQuery is { Length: > 0 } listQuery
            ? $"{WorkPicker}?{listQuery}"
            : WithQuery(
                WorkPicker,
                (WorkListQuery.ImagesKey, YesNoSelect.Yes),
                (WorkPickerTrail.ModeKey, trail.ModeValue)
            );

    public static string WorkPickerImages(WorkId id, WorkPickerTrail trail) =>
        WorkPickerImages(id.Value.ToString(CultureInfo.InvariantCulture), trail);

    // with a placeholder for a script to put a work's id in
    public static string WorkPickerImages(string workId, WorkPickerTrail trail) =>
        WithQuery(
            $"{WorkPicker}/{workId}",
            (WorkPickerTrail.ModeKey, trail.ModeValue),
            (WorkPickerTrail.ListKey, trail.ListQuery)
        );

    public static string WorkPickerChoice(
        WorkId id,
        string storageKey,
        WorkPickerTrail trail
    ) =>
        WithQuery(
            $"{WorkPicker}/{id.Value}",
            (WorkPickerImageKey, storageKey),
            (WorkPickerTrail.ModeKey, trail.ModeValue),
            (WorkPickerTrail.ListKey, trail.ListQuery)
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
