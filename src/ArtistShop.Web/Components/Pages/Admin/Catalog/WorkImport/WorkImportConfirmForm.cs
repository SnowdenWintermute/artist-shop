using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkImport;

// every value arrives in a hidden field written by the review, so the server checks the text again
// rather than trusting it
public class WorkImportConfirmForm : WorkImportSettingsForm
{
    // a file within the byte limit never decodes to more characters than that
    [Required]
    [MaxLength(ImportLimits.FileMaximumBytes)]
    public string? CsvText { get; set; }

    // the plan the artist reviewed; a different plan now means something changed
    [Required]
    public string? Fingerprint { get; set; }

    public static WorkImportConfirmForm ForReview(
        WorkImportSettingsForm settings,
        string csvText,
        string fingerprint
    )
    {
        var form = new WorkImportConfirmForm { CsvText = csvText, Fingerprint = fingerprint };
        form.CopySettingsFrom(settings);
        return form;
    }
}
