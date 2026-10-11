using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

// Turns away an image too big to process, and otherwise waits for room to process it. Disposing the
// lease hands the room back
public static class ImageProcessingAdmission
{
    public static async Task<IDisposable> AcquireAsync(
        ImageHeader header,
        ImageProcessingLimiter limiter,
        ImageProcessingSettings settings,
        CancellationToken cancellationToken
    )
    {
        // checked first, so the estimate below only ever sees sizes that fit in an int
        if ((long)header.Width * header.Height > settings.MaximumMegapixels * Units.PixelsPerMegapixel)
        {
            throw new ImageTooLargeException($"Images must be {settings.MaximumMegapixels} megapixels or smaller.");
        }

        var megabytes = EstimateMegabytes(header, settings);

        // a small server's budget can be below what the megapixel limit allows
        if (megabytes > limiter.MemoryBudgetMegabytes)
        {
            throw new ImageTooLargeException(
                $"This image is too large for this server to process ({header.Width} x {header.Height} pixels)."
            );
        }

        // waits here while other images use the slots or the memory
        return await limiter.AcquireAsync(megabytes, cancellationToken);
    }

    // decoding, resizing and encoding hold several copies of the pixels at once
    private static int EstimateMegabytes(ImageHeader header, ImageProcessingSettings settings) =>
        (int)
            Math.Max(
                1,
                Math.Ceiling(header.DecodedBytes * settings.MemoryEstimateMultiplier / Units.BytesPerMebibyte)
            );
}
