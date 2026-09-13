using NetVips;

namespace ArtistShop.Web.Images;

public record StoredImage(string StorageKey, ProcessedImage Processed);

// Saves an uploaded image and generates its variants
public class ImageUploadStore(ImageStorage imageStorage, ImageProcessor imageProcessor)
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
            return new StoredImage(storageKey, imageProcessor.Process(storageKey));
        }
        catch (Exception exception) when (exception is VipsException or ImageTooSmallException)
        {
            imageStorage.Delete(storageKey);
            // implicitly throws the same caught exception
            throw;
        }
    }
}
