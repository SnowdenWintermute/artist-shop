namespace ArtistShop.Web.Domain.Publishing;

public record UploadedImageId(int Value);

// An image in the library. AltText is empty when the artist cleared it
public record UploadedImage(
    UploadedImageId Id,
    string StorageKey,
    string OriginalFileName,
    int Width,
    int Height,
    string BlurDataUri,
    string AltText,
    DateTimeOffset UploadedAt
);
