namespace ArtistShop.Web.Domain.Publishing;

// A library image's alt text, trimmed as it's saved. Empty when the artist cleared it
public sealed record AltText
{
    public string Value { get; }

    private AltText(string value) => Value = value;

    public static AltText? Read(string text) => ProblemWith(text) is null ? new AltText(text.Trim()) : null;

    // null for alt text that can be saved; otherwise what's wrong with it, said to the artist
    public static string? ProblemWith(string text) =>
        text.Trim().Length > ArtistShopLimits.UploadedImageAltTextMaximumLength
            ? $"Alt text can be at most {ArtistShopLimits.UploadedImageAltTextMaximumLength} characters."
            : null;
}
