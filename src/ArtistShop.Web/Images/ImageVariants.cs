namespace ArtistShop.Web.Images;

public enum ImageVariantFormat : byte
{
    Avif = 1,
    Webp = 2,
}

// the resized copies ImageProcessor writes for every upload, and how they are named
public static class ImageVariants
{
    // @TODO once frontend gallery grid exists, measure the size of the elements
    // and derive these values from it
    public static readonly int[] Widths = [400, 800, 1600];

    public static int SmallestWidth => Widths[0];

    // the small preview the admin lists and the upload rows show
    public const int AdminThumbnailWidth = 400;

    public static string FileName(int width, ImageVariantFormat format) =>
        format switch
        {
            ImageVariantFormat.Avif => $"{width}.avif",
            ImageVariantFormat.Webp => $"{width}.webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

    // variants are never upscaled, so an image only has the widths up to its own
    public static int[] WidthsFor(int imageWidth) => [.. Widths.Where(width => width <= imageWidth)];

    // the largest variant that exists and isn't wider than wanted; the smallest variant when even
    // that is wider than wanted
    public static int LargestWidthUpTo(int imageWidth, int wantedWidth)
    {
        var existingWidths = WidthsFor(imageWidth);

        if (existingWidths.Length is 0)
        {
            // ImageProcessor turns away images narrower than the smallest variant
            throw new InvalidOperationException($"An image {imageWidth} pixels wide has no variants.");
        }

        return existingWidths.LastOrDefault(width => width <= wantedWidth, existingWidths[0]);
    }
}
