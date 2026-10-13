namespace ArtistShop.Web.Publishing;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

// Text from the editor, a post's or an article's, ready to show: parsed, with the images its work
// embeds name, by storage key
public record LoadedPostDocument(PostDocument Document, IReadOnlyDictionary<string, WorkImageWithWork> WorkImages)
{
    public static async Task<LoadedPostDocument> LoadAsync(PostBody body, ImageStorage imageStorage, WorkRepository workRepository)
    {
        var document = Parse(body, imageStorage);

        return new LoadedPostDocument(document, await GetWorkImagesAsync([document], workRepository));
    }

    // A save refuses an uploaded image whose file is gone, but text saved before that check could
    // still name one
    public static PostDocument Parse(PostBody body, ImageStorage imageStorage) =>
        PostImageFiles.WithoutMissing(PostDocumentParser.Parse(body), imageStorage);

    // every work embed's image in one query, however many documents there are
    public static async Task<IReadOnlyDictionary<string, WorkImageWithWork>> GetWorkImagesAsync(
        IEnumerable<PostDocument> documents,
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
