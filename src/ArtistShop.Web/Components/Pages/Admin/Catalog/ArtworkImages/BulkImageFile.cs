namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImages;

public class BulkImageFile(string id, string path, string artworkName)
{
    public string Id { get; } = id;
    public string Path { get; } = path;
    public string ArtworkName { get; } = artworkName;
    public BulkImageOutcome Outcome { get; set; }
    public string? Error { get; set; }
}
