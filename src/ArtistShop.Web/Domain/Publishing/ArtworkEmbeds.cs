using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Domain.Publishing;

public static class ArtworkEmbeds
{
    // ArtworkImages holds every artwork embed's image by storage key, loaded together. Null leaves
    // the embed out: its image was deleted, or now belongs to another artwork than the one the
    // embed names, which no edit can do, but the parser can't rule out
    public static ArtworkImageWithArtwork? SourceOf(
        ArtworkEmbedBlock embed,
        IReadOnlyDictionary<string, ArtworkImageWithArtwork> artworkImages
    ) =>
        artworkImages.TryGetValue(embed.StorageKey, out var source) && source.ArtworkId == embed.ArtworkId
            ? source
            : null;
}
