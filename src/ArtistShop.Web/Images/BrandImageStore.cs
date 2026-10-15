using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Images;

// The website's logos and favicon. Callers don't know where anything is kept: the served files are
// in the site's database, and each original is in the site's image storage, read only by export
public class BrandImageStore(
    BrandImageRepository repository,
    ImageStorage imageStorage,
    BrandImageProcessor processor,
    ILogger<BrandImageStore> logger
)
{
    public Task<Dictionary<BrandImageType, BrandImage>> GetAllAsync() => repository.GetAllAsync();

    public Task<List<BrandImageOriginal>> GetAllOriginalsAsync() => repository.GetAllOriginalsAsync();

    // null when it's gone
    public Stream? OpenOriginal(BrandImageOriginal original) => imageStorage.OpenOriginal(original.StorageKey);

    public Task<(BrandImageFormat Format, byte[] Bytes)?> GetFileAsync(string sha256) => repository.GetFileAsync(sha256);

    // Shown at once: there's no saving step, so nothing uploaded is ever left unreferenced except by a crash,
    // which the orphan sweep tidies
    public async Task ReplaceAsync(
        BrandImageType type,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken
    )
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var original = buffer.ToArray();

        // before anything is saved, so a refused image leaves nothing behind
        var files = await processor.ProcessAsync(original, type, cancellationToken);

        var storageKey = ImageStorage.NewStorageKey();
        buffer.Position = 0;
        var sha256 = await imageStorage.SaveOriginalAsync(buffer, storageKey, cancellationToken);
        string? replacedStorageKey;

        try
        {
            replacedStorageKey = await repository.ReplaceAsync(type, storageKey, originalFileName, sha256, files);
        }
        catch
        {
            imageStorage.Delete(storageKey);
            throw;
        }

        DeleteOriginal(replacedStorageKey);
    }

    public async Task RemoveAsync(BrandImageType type) => DeleteOriginal(await repository.RemoveAsync(type));

    // after the database no longer names it, so a failure only leaves a file for the sweep
    private void DeleteOriginal(string? storageKey)
    {
        if (storageKey is null)
        {
            return;
        }

        try
        {
            imageStorage.Delete(storageKey);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete replaced brand image original {StorageKey}", storageKey);
        }
    }
}
