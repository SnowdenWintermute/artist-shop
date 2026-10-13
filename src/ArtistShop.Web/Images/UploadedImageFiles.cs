namespace ArtistShop.Web.Images;

using ArtistShop.Web.Domain.Publishing;

// An image uploaded into the editor is named only in the text, so the orphan sweep can remove
// its file while the text sits unsaved, or in a backup restored too late
public static class UploadedImageFiles
{
    public static IReadOnlyList<UploadedImageEmbedBlock> MissingFrom(RichTextDocument document, ImageStorage imageStorage) =>
        [.. document.Blocks.OfType<UploadedImageEmbedBlock>().Where(image => IsMissing(image, imageStorage))];

    // Left out rather than showing a blur with nothing over it, as a work embed whose image
    // was deleted is
    public static RichTextDocument WithoutMissing(RichTextDocument document, ImageStorage imageStorage) =>
        document with
        {
            Blocks = [.. document.Blocks.Where(block => block is not UploadedImageEmbedBlock image || !IsMissing(image, imageStorage))],
        };

    private static bool IsMissing(UploadedImageEmbedBlock image, ImageStorage imageStorage) =>
        !imageStorage.OriginalExists(image.StorageKey);
}
