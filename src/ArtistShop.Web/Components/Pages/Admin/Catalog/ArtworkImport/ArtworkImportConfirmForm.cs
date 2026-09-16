using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImport;

// every value arrives in a hidden field written by the review, so the server checks the text again
// rather than trusting it
public class ArtworkImportConfirmForm : ArtworkImportSettingsForm
{
    // a file within the byte limit never decodes to more characters than that
    [Required]
    [MaxLength(ArtworkImportLimits.FileMaximumBytes)]
    public string? CsvText { get; set; }

    // the plan the artist reviewed; a different plan now means something changed
    [Required]
    public string? Fingerprint { get; set; }

    public static ArtworkImportConfirmForm ForReview(
        ArtworkImportSettingsForm settings,
        string csvText,
        string fingerprint
    )
    {
        var form = new ArtworkImportConfirmForm { CsvText = csvText, Fingerprint = fingerprint };
        form.CopySettingsFrom(settings);
        return form;
    }
}
