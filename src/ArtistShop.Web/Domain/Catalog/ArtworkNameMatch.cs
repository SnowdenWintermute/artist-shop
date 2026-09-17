namespace ArtistShop.Web.Domain.Catalog;

// the values must match the match types in dbo.GetArtworkNameMatches and
// dbo.AttachPrimaryImageToImagelessArtworkByName
public enum ArtworkNameMatchType : byte
{
    OneImagelessArtwork = 1,
    NoArtwork = 2,
    SeveralArtworks = 3,
    ArtworkWithImages = 4,
}

// from the attach procedure, OneImagelessArtwork means the image was attached to it
public record ArtworkNameMatch(ArtworkNameMatchType Type, IReadOnlyList<ArtworkId> ArtworkIds);
