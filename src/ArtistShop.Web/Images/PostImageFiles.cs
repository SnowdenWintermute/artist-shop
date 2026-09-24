namespace ArtistShop.Web.Images;

using ArtistShop.Web.Domain.Publishing;

// An image uploaded into a post is named only in the post's body, so the orphan sweep can remove
// its file while the post sits unsaved, or in a backup restored too late
public static class PostImageFiles
{
    public static IReadOnlyList<PostImageEmbedBlock> MissingFrom(PostDocument document, ImageStorage imageStorage) =>
        [.. document.Blocks.OfType<PostImageEmbedBlock>().Where(image => IsMissing(image, imageStorage))];

    // Left out rather than showing a blur with nothing over it, as an artwork embed whose image
    // was deleted is
    public static PostDocument WithoutMissing(PostDocument document, ImageStorage imageStorage) =>
        document with
        {
            Blocks = [.. document.Blocks.Where(block => block is not PostImageEmbedBlock image || !IsMissing(image, imageStorage))],
        };

    private static bool IsMissing(PostImageEmbedBlock image, ImageStorage imageStorage) =>
        !imageStorage.OriginalExists(image.StorageKey);
}
