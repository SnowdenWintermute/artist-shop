using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.CatalogImport;

// the artwork type and vocabulary imports' file and list separator. Form posts create this, and they
// need exactly one public constructor
public class CatalogImportCheckForm : ServerValidatedForm, IImportFileForm
{
    public IFormFile? File { get; set; }

    [MaxLength(ImportLimits.FileMaximumBytes)]
    public string? KeptCsvText { get; set; }

    public string? KeptFileName { get; set; }

    [Required(ErrorMessage = "Choose a list separator.")]
    [RegularExpression(ImportListSeparator.Pattern, ErrorMessage = ImportListSeparator.Message)]
    public string? ListSeparator { get; set; } = ";";

    public void AddFileError(string message) => AddServerError(nameof(File), message);
}
