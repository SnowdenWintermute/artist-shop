
namespace ArtistShop.Web.Domain.Catalog;

public record SeriesId(int Value);

public record SeriesName(string Value);

public record SeriesSlug(string Value)
{
    public static SeriesSlug FromName(string name) => new(ArtistShopSlug.FromName(name));
}

public record Series(SeriesId Id, SeriesName Name, SeriesSlug Slug);

// Cover is null when no artwork in the series has an image
public record SeriesWithCover(
    SeriesId Id,
    SeriesName Name,
    SeriesSlug Slug,
    int ArtworkCount,
    ArtworkImage? Cover
);

public record SeriesArtwork(
    ArtworkId Id,
    ArtworkName Name,
    ArtworkTypeName ArtworkTypeName,
    bool IsCover,
    ArtworkImage? PrimaryImage
);

public record SeriesWithArtworks(
    SeriesId Id,
    SeriesName Name,
    SeriesSlug Slug,
    IReadOnlyList<SeriesArtwork> Artworks
);
