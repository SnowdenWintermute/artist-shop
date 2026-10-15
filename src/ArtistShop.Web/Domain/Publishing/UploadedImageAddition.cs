namespace ArtistShop.Web.Domain.Publishing;

// An image going into the library. Sha256 is the original's, in lower case hex
public record UploadedImageAddition(
    string StorageKey,
    string OriginalFileName,
    string Sha256,
    int Width,
    int Height,
    string BlurDataUri,
    string AltText
);
