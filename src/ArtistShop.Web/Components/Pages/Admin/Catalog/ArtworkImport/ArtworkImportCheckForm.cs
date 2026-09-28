using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Pages.Admin.Catalog.CatalogImport;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkImport;

// form posts create this, and they need exactly one public constructor
public class ArtworkImportCheckForm : ArtworkImportSettingsForm, IImportFileForm
{
    public IFormFile? File { get; set; }

    [MaxLength(ImportLimits.FileMaximumBytes)]
    public string? KeptCsvText { get; set; }

    public string? KeptFileName { get; set; }

    public void AddFileError(string message) => AddServerError(nameof(File), message);
}
