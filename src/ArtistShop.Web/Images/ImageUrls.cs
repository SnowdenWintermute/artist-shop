namespace ArtistShop.Web.Images;

// the one place that knows where variants are served from, so moving them (to a CDN or an image
// service) changes only this file
public static class ImageUrls
{
    // Program.cs serves the variants folder at this path
    public const string VariantsRequestPath = "/media";

    public static string Variant(string storageKey, int imageWidth, int wantedWidth, ImageVariantFormat format) =>
        $"{VariantsRequestPath}/{storageKey}/"
        + ImageVariants.FileName(ImageVariants.LargestWidthUpTo(imageWidth, wantedWidth), format);
}
