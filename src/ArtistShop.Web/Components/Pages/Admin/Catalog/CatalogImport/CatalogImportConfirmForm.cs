using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.CatalogImport;

// every value arrives in a hidden field written by the review, so the server plans the text again
// rather than trusting it
public class CatalogImportConfirmForm : ServerValidatedForm
{
    // a file within the byte limit never decodes to more characters than that
    [Required]
    [MaxLength(ImportLimits.FileMaximumBytes)]
    public string? CsvText { get; set; }

    // the plan the artist reviewed; a different plan now means something changed
    [Required]
    public string? Fingerprint { get; set; }

    [Required]
    [RegularExpression(ImportListSeparator.Pattern)]
    public string? ListSeparator { get; set; }
}
