namespace ArtistShop.Web.Publishing;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// What the image embeds of one or more texts show, by storage key: each work embed's work image,
// and each uploaded image embed's library image. A deleted image has no entry
public record RichTextImages(
    IReadOnlyDictionary<string, WorkImageWithWork> WorkImages,
    IReadOnlyDictionary<string, UploadedImage> UploadedImages
)
{
    // every embed's image in one query of each kind, however many documents there are
    public static async Task<RichTextImages> LoadAsync(
        IReadOnlyCollection<RichTextDocument> documents,
        WorkRepository workRepository,
        UploadedImageRepository uploadedImageRepository
    ) =>
        new(
            await workRepository.GetImagesByStorageKeyAsync(StorageKeysOf<WorkEmbedBlock>(documents, embed => embed.StorageKey)),
            await uploadedImageRepository.GetByStorageKeysAsync(UploadedImageKeysOf(documents))
        );

    // the library images the documents show, as a page loads them
    public static string[] UploadedImageKeysOf(IEnumerable<RichTextDocument> documents) =>
        StorageKeysOf<UploadedImageEmbedBlock>(documents, embed => embed.StorageKey);

    private static string[] StorageKeysOf<TEmbed>(IEnumerable<RichTextDocument> documents, Func<TEmbed, string> storageKey) =>
        [.. documents.SelectMany(document => document.Blocks.OfType<TEmbed>()).Select(storageKey).Distinct()];
}

// Text from the editor, a post's or a text component's, ready to show: parsed, with the images its
// embeds name
public record LoadedRichText(RichTextDocument Document, RichTextImages Images)
{
    public static async Task<LoadedRichText> LoadAsync(
        RichText body,
        WorkRepository workRepository,
        UploadedImageRepository uploadedImageRepository
    )
    {
        var document = RichTextParser.Parse(body);

        return new LoadedRichText(document, await RichTextImages.LoadAsync([document], workRepository, uploadedImageRepository));
    }
}
