namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

// The image the slide list's pickers chose, posted at once. Form posts create this, so it keeps a single public
// constructor
public class AddSlideInput : ImagePickerForm
{
    // False when the form says by the field why: none chosen, which only a hand-written post sends, or the image
    // gone since the picker showed it
    public async Task<bool> CheckAsync(UploadedImageRepository uploadedImageRepository, WorkRepository workRepository, SiteWording wording)
    {
        if (ChosenImageSource is null)
        {
            AddServerError(nameof(ImageStorageKey), "Choose an image.");
            return false;
        }

        return await CheckChosenImageAsync(uploadedImageRepository, workRepository, wording);
    }

    public ImageSource ToSource() => ChosenImageSource ?? throw new InvalidOperationException("No image was chosen.");
}
