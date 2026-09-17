using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

public record StoredImage(string StorageKey, ProcessedImage Processed);

// Saves an uploaded image and generates its variants
public class ImageUploadStore(
    ImageStorage imageStorage,
    ImageProcessor imageProcessor,
    ImageProcessingLimiter processingLimiter,
    ImageProcessingSettings processingSettings
)
{
    public async Task<StoredImage> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        // version 7 embeds timestamp when created so they can be sorted by date
        // and avoid database fragmenting if stored there
        // "n" gives 32 hex characters with no dash or braces
        var storageKey = Guid.CreateVersion7().ToString("n");
        await imageStorage.SaveOriginalAsync(content, storageKey, cancellationToken);

        try
        {
            var header = imageProcessor.ReadHeader(storageKey);

            // checked first, so the estimate below only ever sees sizes that fit in an int
            if ((long)header.Width * header.Height > processingSettings.MaximumMegapixels * Units.PixelsPerMegapixel)
            {
                throw new ImageTooLargeException(
                    $"Images must be {processingSettings.MaximumMegapixels} megapixels or smaller."
                );
            }

            var megabytes = EstimateMegabytes(header);

            // a small server's budget can be below what the megapixel limit allows
            if (megabytes > processingLimiter.MemoryBudgetMegabytes)
            {
                throw new ImageTooLargeException(
                    $"This image is too large for this server to process ({header.Width} x {header.Height} pixels)."
                );
            }

            // waits here while other images use the slots or the memory
            using var lease = await processingLimiter.AcquireAsync(megabytes, cancellationToken);
            return new StoredImage(storageKey, imageProcessor.Process(storageKey));
        }
        catch
        {
            // nothing references a file whose processing didn't finish, including a cancelled
            // or turned-away upload, so it goes now rather than waiting for the sweep
            imageStorage.Delete(storageKey);
            // implicitly throws the same caught exception
            throw;
        }
    }

    // decoding, resizing and encoding hold several copies of the pixels at once
    private int EstimateMegabytes(ImageHeader header) =>
        (int)
            Math.Max(
                1,
                Math.Ceiling(
                    header.DecodedBytes
                        * processingSettings.MemoryEstimateMultiplier
                        / Units.BytesPerMebibyte
                )
            );
}
