namespace ArtistShop.Web.Components.Pages.Admin.TextEditor;

using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

// What refuses a save of the editor's text, for every form that holds it. The noun is what the text
// belongs to, such as "post"
public static class PostBodyChecks
{
    public static string NotInEditorFormat(string noun) =>
        $"The {noun}'s text didn't arrive in the editor's format, so nothing was saved.";

    // uploads the orphan sweep removed while the text sat unsaved, and links the page would drop
    public static IReadOnlyList<string> SaveProblems(PostBody body, ImageStorage imageStorage, string noun) =>
        [
            .. PostImageFiles
                .MissingFrom(PostDocumentParser.Parse(body), imageStorage)
                .Select(image => MissingImageProblem(image, noun)),
            .. PostDocumentParser.LinksDropped(body).Select(PostDocumentParser.DroppedLinkProblem),
        ];

    private static string MissingImageProblem(PostImageEmbedBlock image, string noun)
    {
        var named = image.Alt is "" ? "An image" : $"The image \"{image.Alt}\"";

        return $"{named} is no longer on the server because the {noun} wasn't saved in time. Remove it and upload it again.";
    }
}
