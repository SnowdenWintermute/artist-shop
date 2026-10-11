namespace ArtistShop.Web.Components.Pages.Admin.Website.Branding;

// the upload form's model, for its field's label and id: the file itself posts to the upload endpoint
public class BrandImageUploadForm
{
    public IFormFile? File { get; set; }
}
