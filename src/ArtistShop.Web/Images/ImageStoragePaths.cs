namespace ArtistShop.Web.Images;

public class ImageStoragePaths(string rootPath)
{
    public string Originals { get; } = Path.Combine(rootPath, "originals");
    public string Variants { get; } = Path.Combine(rootPath, "variants");
}
