using System.Text.Json.Serialization;
using ArtistShop.Web.Components.Tables;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Components.Pages.Admin.Publishing.Images;

// One library image in the Images page's table. Settable, since the table is an island whose
// parameters are rebuilt from JSON, and AltText is what the row edits
public class ImageTableRow : EditableTableRow
{
    public required UploadedImage Image { get; set; }
    public required int UsedInCount { get; init; }

    // the field's value, kept in Image so its Name follows the edit; Image carries it in the JSON
    [JsonIgnore]
    public string AltText
    {
        get => Image.AltText;
        set => Image = Image with { AltText = value };
    }

    public static ImageTableRow From(UploadedImageListItem item) => new() { Image = item.Image, UsedInCount = item.UsedInCount };
}
