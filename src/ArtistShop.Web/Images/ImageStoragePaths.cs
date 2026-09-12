namespace ArtistShop.Web.Images;

public class ImageStoragePaths(string rootPath)
{
    public string Originals { get; } = Path.Combine(rootPath, "originals");
    public string Variants { get; } = Path.Combine(rootPath, "variants");

    public bool OriginalExists(string? storageKey)
    {
        // "N" is the 32 hexadecimal characters, no dashes, format
        // same format our upload endpoint uses
        // "out _" discards parsed value, we only care if it did parse
        return Guid.TryParseExact(storageKey, "N", out _)
            && File.Exists(Path.Combine(Originals, storageKey));
    }
}
