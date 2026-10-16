using ArtistShop.Web.Components.Tables;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Images;

// One library image in the Images page's table. Settable, since the table is an island whose
// parameters are rebuilt from JSON, and AltText is what the row edits
public class ImageTableRow : EditableTableRow
{
    public required string StorageKey { get; init; }
    public required string OriginalFileName { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string BlurDataUri { get; init; }
    public required string AltText { get; set; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required int UsedInCount { get; init; }

    public static ImageTableRow From(UploadedImageListItem item) =>
        new()
        {
            StorageKey = item.Image.StorageKey,
            OriginalFileName = item.Image.OriginalFileName,
            Width = item.Image.Width,
            Height = item.Image.Height,
            BlurDataUri = item.Image.BlurDataUri,
            AltText = item.Image.AltText,
            UploadedAt = item.Image.UploadedAt,
            UsedInCount = item.UsedInCount,
        };

    // what TileImage draws a thumbnail from, a work's image or not
    public WorkImage Thumbnail => new(StorageKey, OriginalFileName, Width, Height, BlurDataUri);
}
