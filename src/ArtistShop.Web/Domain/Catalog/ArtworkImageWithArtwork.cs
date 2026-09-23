namespace ArtistShop.Web.Domain.Catalog;

// One image and the artwork it belongs to, for a page that shows a single image of an artwork
// rather than the artwork itself, such as an embed in a post. ImageNumber is its place on the
// artwork page, counted from one as that page's address does
public record ArtworkImageWithArtwork(
    ArtworkId ArtworkId,
    ArtworkLink Artwork,
    ArtworkImage Image,
    int ImageNumber
);
