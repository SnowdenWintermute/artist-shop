using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImport;

// form posts create this, and they need exactly one public constructor
public class ArtworkImportCheckForm : ArtworkImportSettingsForm
{
    // a browser never keeps a chosen file across a page load, so this is null on every post after the
    // first unless the artist picks the file again
    public IFormFile? File { get; set; }

    // the last file this form read, carried in hidden fields so a failed check doesn't lose it
    [MaxLength(ArtworkImportLimits.FileMaximumBytes)]
    public string? KeptCsvText { get; set; }

    public string? KeptFileName { get; set; }

    // a form posted with no file chosen still sends an empty, unnamed file
    public IFormFile? ChosenFile => File is { FileName.Length: > 0 } ? File : null;

    public void KeepFile(string fileName, string csvText)
    {
        KeptFileName = fileName;
        KeptCsvText = csvText;
    }

    public void ForgetKeptFile()
    {
        KeptFileName = null;
        KeptCsvText = null;
    }

    public void AddFileError(string message) => AddServerError(nameof(File), message);
}
