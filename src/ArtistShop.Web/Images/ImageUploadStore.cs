using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

// Sha256 is the original's, in lower case hex
public record StoredImage(string StorageKey, string Sha256, ProcessedImage Processed)
{
    public WorkImage ToWorkImage(string originalFileName) =>
        new(StorageKey, originalFileName, Processed.Width, Processed.Height, Processed.BlurDataUri);

    // its alt text starts as the file name without its extension, which the artist rewrites. Upload
    // validation keeps file names shorter than alt text can be
    public UploadedImageAddition ToUploadedImage(string originalFileName) =>
        new(
            StorageKey,
            originalFileName,
            Sha256,
            Processed.Width,
            Processed.Height,
            Processed.BlurDataUri,
            Unwrap.Value(AltText.Read(Path.GetFileNameWithoutExtension(originalFileName)))
        );
}

// Saves an uploaded image and makes its first size, and hands the rest to the queue
public class ImageUploadStore(
    ImageStorage imageStorage,
    ImageProcessor imageProcessor,
    ImageProcessingLimiter processingLimiter,
    ImageProcessingSettings processingSettings,
    ImageSizeQueue imageSizeQueue
)
{
    public async Task<StoredImage> SaveAsync(Stream content, int minimumWidth, CancellationToken cancellationToken)
    {
        var storageKey = ImageStorage.NewStorageKey();
        var sha256 = await imageStorage.SaveOriginalAsync(content, storageKey, cancellationToken);

        try
        {
            ProcessedImage processed;

            using (
                await ImageProcessingAdmission.AcquireAsync(
                    imageProcessor.ReadHeader(storageKey),
                    processingLimiter,
                    processingSettings,
                    cancellationToken
                )
            )
            {
                processed = imageProcessor.MakeFirstSize(storageKey, minimumWidth);
            }

            imageSizeQueue.Enqueue(new ImageSizeJob(imageStorage, storageKey, ImageVariants.LaterWidths(processed.Width)));

            return new StoredImage(storageKey, sha256, processed);
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
}
