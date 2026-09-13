namespace ArtistShop.Web.Images;

public class ImageStorage(string rootPath)
{
    public string Originals { get; } = Path.Combine(rootPath, "originals");
    public string Variants { get; } = Path.Combine(rootPath, "variants");

    public string OriginalPath(string storageKey) => Path.Combine(Originals, storageKey);

    public string VariantDirectory(string storageKey) => Path.Combine(Variants, storageKey);

    public bool OriginalExists(string? storageKey)
    {
        // "N" is the 32 hexadecimal characters, no dashes, format
        // same format our upload endpoint uses
        // "out _" discards parsed value, we only care if it did parse
        return Guid.TryParseExact(storageKey, "N", out _) && File.Exists(OriginalPath(storageKey));
    }

    public async Task SaveOriginalAsync(
        Stream contentStream,
        string storageKey,
        CancellationToken cancellationToken
    )
    {
        await using var destination = File.Create(OriginalPath(storageKey));
        await contentStream.CopyToAsync(destination, cancellationToken);
    }

    public void Delete(string storageKey)
    {
        // trying to delete a directory that doesn't exist
        // will throw
        var variantDirectory = VariantDirectory(storageKey);
        if (Directory.Exists(variantDirectory))
        {
            Directory.Delete(variantDirectory, recursive: true);
        }

        // delete originals last because sweep looks for originals
        // to determine orphans
        File.Delete(OriginalPath(storageKey));
    }
}
