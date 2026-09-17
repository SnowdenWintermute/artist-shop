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

public record ArtworkNameMatch(ArtworkNameMatchType Type, IReadOnlyList<ArtworkId> ArtworkIds);

// what the attach procedure did with one file. It classifies the name the same way the pre-check
// does, and the one-imageless-artwork case is the one where the image was attached
public record ImageAttachResult(ArtworkNameMatchType MatchType, IReadOnlyList<ArtworkId> ArtworkIds)
{
    public bool Attached => MatchType is ArtworkNameMatchType.OneImagelessArtwork;
}
