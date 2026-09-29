using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

// Saves one planned post once the page's script has uploaded its files
public static class PostImporter
{
    public const string ImageNotUploadedProblem = "One of its images didn't upload.";

    // null once saved, otherwise why it wasn't. UploadsByFileId is what the upload endpoint answered
    // for each file, as the browser reported it
    public static async Task<string?> SaveAsync(
        PostImportItem item,
        IReadOnlyDictionary<string, ImageUploadResult> uploadsByFileId,
        PostRepository posts,
        ImageStorage imageStorage
    )
    {
        var document = Unwrap.Value(item.Document);
        var uploadsByPlaceholder = new Dictionary<string, ImageUploadResult>();

        foreach (var (placeholder, fileId) in item.FileIdsByPlaceholder)
        {
            if (!uploadsByFileId.TryGetValue(fileId, out var upload))
            {
                return ImageNotUploadedProblem;
            }

            uploadsByPlaceholder[placeholder] = upload;
        }

        var body = PostImportPlanner.Finish(document, uploadsByPlaceholder);

        // The browser said where each upload went, so each must be a file here: the parser drops an
        // image whose key isn't a storage key's shape, and a save refuses one whose file is gone
        var saved = PostDocumentParser.Parse(body);

        if (
            saved.Blocks.OfType<PostImageEmbedBlock>().Count() != document.Blocks.OfType<PostImageEmbedBlock>().Count()
            || PostImageFiles.MissingFrom(saved, imageStorage).Count > 0
        )
        {
            return ImageNotUploadedProblem;
        }

        try
        {
            await posts.AddImportedAsync(Unwrap.Value(item.Title), Unwrap.Value(item.Slug), body, item.PublishedAt);
        }
        catch (NameAlreadyInUseException)
        {
            return "A post with this title is already on this website.";
        }

        return null;
    }
}
