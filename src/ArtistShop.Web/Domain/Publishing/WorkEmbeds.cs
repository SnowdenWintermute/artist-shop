using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Domain.Publishing;

public static class WorkEmbeds
{
    // WorkImages holds every work embed's image by storage key, loaded together. Null leaves
    // the embed out: its image was deleted, or now belongs to another work than the one the
    // embed names, which no edit can do, but the parser can't rule out
    public static WorkImageWithWork? SourceOf(
        WorkEmbedBlock embed,
        IReadOnlyDictionary<string, WorkImageWithWork> workImages
    ) =>
        workImages.TryGetValue(embed.StorageKey, out var source) && source.WorkId == embed.WorkId
            ? source
            : null;
}
