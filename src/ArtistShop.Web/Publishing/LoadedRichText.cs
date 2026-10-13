namespace ArtistShop.Web.Publishing;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

// Text from the editor, a post's or a text component's, ready to show: parsed, with the images its work
// embeds name, by storage key
public record LoadedRichText(RichTextDocument Document, IReadOnlyDictionary<string, WorkImageWithWork> WorkImages)
{
    public static async Task<LoadedRichText> LoadAsync(RichText body, ImageStorage imageStorage, WorkRepository workRepository)
    {
        var document = Parse(body, imageStorage);

        return new LoadedRichText(document, await GetWorkImagesAsync([document], workRepository));
    }

    // A save refuses an uploaded image whose file is gone, but text saved before that check could
    // still name one
    public static RichTextDocument Parse(RichText body, ImageStorage imageStorage) =>
        UploadedImageFiles.WithoutMissing(RichTextParser.Parse(body), imageStorage);

    // every work embed's image in one query, however many documents there are
    public static async Task<IReadOnlyDictionary<string, WorkImageWithWork>> GetWorkImagesAsync(
        IEnumerable<RichTextDocument> documents,
        WorkRepository workRepository
    ) =>
        await workRepository.GetImagesByStorageKeyAsync(
            [
                .. documents
                    .SelectMany(document => document.Blocks.OfType<WorkEmbedBlock>())
                    .Select(embed => embed.StorageKey)
                    .Distinct(),
            ]
        );
}
