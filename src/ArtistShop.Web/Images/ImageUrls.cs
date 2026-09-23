using ArtistShop.Web.Domain.Publishing;

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

    // needs no image width, since every image has both embed sizes
    public static string EmbedVariant(string storageKey, EmbedImageSize size) =>
        $"{VariantsRequestPath}/{storageKey}/"
        + ImageVariants.FileName(ImageVariants.EmbedWidth(size), ImageVariantFormat.Avif);

    // every variant this image actually has, each under its true width, so the browser is never
    // handed a narrower file than the number it is choosing by
    public static string SourceSet(string storageKey, int imageWidth, int maximumWidth, ImageVariantFormat format) =>
        string.Join(
            ", ",
            ImageVariants.WidthsFor(imageWidth)
                .Where(width => width <= maximumWidth)
                .Select(width => $"{Variant(storageKey, imageWidth, width, format)} {width}w")
        );
}
