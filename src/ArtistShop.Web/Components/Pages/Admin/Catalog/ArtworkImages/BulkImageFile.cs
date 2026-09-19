using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImages;

public class BulkImageFile(string id, string path, ArtworkName artworkName)
{
    public string Id { get; } = id;
    public string Path { get; } = path;
    public ArtworkName ArtworkName { get; } = artworkName;
    public BulkImageOutcome Outcome { get; set; }
    public string? Error { get; set; }

    // the artworks the name matched, so the report can link to them
    public IReadOnlyList<int> ArtworkIds { get; set; } = [];
}
