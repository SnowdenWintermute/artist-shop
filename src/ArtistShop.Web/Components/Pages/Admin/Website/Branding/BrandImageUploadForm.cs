using ArtistShop.Web.Components.Forms;

namespace ArtistShop.Web.Components.Pages.Admin.Website.Branding;

// Form posts create this, and they need exactly one public constructor
public class BrandImageUploadForm : ServerValidatedForm
{
    public IFormFile? File { get; set; }

    public void AddFileError(string message) => AddServerError(nameof(File), message);
}
