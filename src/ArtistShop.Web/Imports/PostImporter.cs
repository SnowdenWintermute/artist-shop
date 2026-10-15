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
        IReadOnlyDictionary<string, LibraryImageUploadResult> uploadsByFileId,
        PostRepository posts,
        UploadedImageRepository uploadedImages
    )
    {
        var document = Unwrap.Value(item.Document);
        var uploadsByPlaceholder = new Dictionary<string, LibraryImageUploadResult>();

        foreach (var (placeholder, image) in item.ImagesByPlaceholder)
        {
            if (!uploadsByFileId.TryGetValue(image.FileId, out var upload))
            {
                return ImageNotUploadedProblem;
            }

            uploadsByPlaceholder[placeholder] = upload;
        }

        var body = PostImportPlanner.Finish(document, uploadsByPlaceholder);

        // The browser said where each upload went, so each must be in the library here: the parser
        // drops an image whose key isn't a storage key's shape
        var saved = RichTextParser.Parse(body);
        var storageKeys = uploadsByPlaceholder.Values.Select(upload => upload.StorageKey).Distinct().ToList();

        if (
            saved.Blocks.OfType<UploadedImageEmbedBlock>().Count() != document.Blocks.OfType<UploadedImageEmbedBlock>().Count()
            || (await uploadedImages.GetByStorageKeysAsync(storageKeys)).Count != storageKeys.Count
        )
        {
            return ImageNotUploadedProblem;
        }

        foreach (var (placeholder, image) in item.ImagesByPlaceholder)
        {
            await uploadedImages.SetAltTextAsync(uploadsByPlaceholder[placeholder].StorageKey, image.AltText);
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
