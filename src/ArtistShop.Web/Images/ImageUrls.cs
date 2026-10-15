using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Images;

// the one place that knows where variants are served from, so moving them (to a CDN or an image
// service) changes only this file
public static class ImageUrls
{
    // for every served image whose address never gets different bytes, a variant or a brand image:
    // browsers may keep it for a week without asking again
    public const string CacheControl = "public, max-age=604800, immutable";

    // VariantEndpoints serves the current site's variants at this path
    public const string VariantsRequestPath = "/media";

    // readyWidths is ImageSizeQueue.ReadyWidths: only sizes already made
    public static string Variant(string storageKey, IReadOnlyList<int> readyWidths, int wantedWidth, ImageVariantFormat format) =>
        $"{VariantsRequestPath}/{storageKey}/"
        + ImageVariants.FileName(ImageVariants.LargestWidthUpTo(readyWidths, wantedWidth), format);

    // needs no image width, since every work image has both embed sizes
    public static string EmbedVariant(string storageKey, EmbedImageSize size) =>
        $"{VariantsRequestPath}/{storageKey}/"
        + ImageVariants.FileName(ImageVariants.EmbedWidth(size), ImageVariantFormat.Avif);

    // with placeholders for a script to put a storage key and a width in
    public static string VariantTemplate(string storageKey, string width, ImageVariantFormat format) =>
        $"{VariantsRequestPath}/{storageKey}/" + ImageVariants.FileName(width, format);

    // every variant this image has now, each under its true width, so the browser is never
    // handed a narrower file than the number it is choosing by
    public static string SourceSet(string storageKey, IReadOnlyList<int> readyWidths, int maximumWidth, ImageVariantFormat format) =>
        string.Join(
            ", ",
            readyWidths
                .Where(width => width <= maximumWidth)
                .Select(width => $"{VariantsRequestPath}/{storageKey}/{ImageVariants.FileName(width, format)} {width}w")
        );
}
