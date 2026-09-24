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

    // needs no image width, since every artwork image has both embed sizes
    public static string EmbedVariant(string storageKey, EmbedImageSize size) =>
        $"{VariantsRequestPath}/{storageKey}/"
        + ImageVariants.FileName(ImageVariants.EmbedWidth(size), ImageVariantFormat.Avif);

    // A post's own image may be narrower than the size picked, and then this is its copy at its own
    // width, which the page stretches to the size
    public static string PostImageVariant(string storageKey, int imageWidth, EmbedImageSize size) =>
        Variant(storageKey, imageWidth, ImageVariants.EmbedWidth(size), ImageVariantFormat.Avif);

    // with placeholders for a script to put a storage key and a width in
    public static string VariantTemplate(string storageKey, string width, ImageVariantFormat format) =>
        $"{VariantsRequestPath}/{storageKey}/" + ImageVariants.FileName(width, format);

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
