namespace ArtistShop.Web.Components.Pages.Admin.TextEditor;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Publishing;

// What refuses a save of the editor's text, for every form that holds it. The noun is what the text
// belongs to, such as "post"
public static class RichTextChecks
{
    public static string NotInEditorFormat(string noun) =>
        $"The {noun}'s text didn't arrive in the editor's format, so nothing was saved.";

    // images deleted from the library while the text sat unsaved, and links the page would drop
    public static async Task<IReadOnlyList<string>> SaveProblemsAsync(
        RichText body,
        UploadedImageRepository uploadedImageRepository,
        string noun
    )
    {
        var storageKeys = LibraryImageFields.StorageKeysIn(body);
        var found = await uploadedImageRepository.GetByStorageKeysAsync(storageKeys);

        return
        [
            .. storageKeys.Where(storageKey => !found.ContainsKey(storageKey)).Select(_ => MissingImageProblem(noun)),
            .. RichTextParser.LinksDropped(body).Select(RichTextParser.DroppedLinkProblem),
        ];
    }

    private static string MissingImageProblem(string noun) =>
        $"An image in the {noun} was deleted from the image library. Remove it, then save again.";
}
