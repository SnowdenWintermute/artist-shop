using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A form holding an ImageSourceField, whose pickers set these by name: a library image's key, or with
// ImageWorkId one of that work's images (null there for its main image). Both null for none chosen
public abstract class ImagePickerForm : ServerValidatedForm
{
    public string? ImageStorageKey { get; set; }

    public int? ImageWorkId { get; set; }

    // a saved source as these fields hold it
    protected static string? ImageStorageKeyOf(ImageSource? source) =>
        source switch
        {
            ImageSource.Library library => library.StorageKey,
            ImageSource.Work work => work.StorageKey,
            _ => null,
        };

    protected static int? ImageWorkIdOf(ImageSource? source) => source is ImageSource.Work work ? work.WorkId.Value : null;

    // the hidden field posts an empty value for none
    private string? ChosenImageKey => string.IsNullOrEmpty(ImageStorageKey) ? null : ImageStorageKey;

    protected ImageSource? ChosenImageSource =>
        ImageWorkId is int workId ? new ImageSource.Work(new WorkId(workId), ChosenImageKey)
        : ChosenImageKey is { } storageKey ? new ImageSource.Library(storageKey)
        : null;

    // The chosen image, which may have been deleted, or taken off its work, since the picker showed it.
    // False when the form says by the field why. A work's main image is kept as no image of it picked, so
    // what shows it follows the work's main image when it changes
    protected async Task<bool> CheckChosenImageAsync(
        UploadedImageRepository uploadedImageRepository,
        WorkRepository workRepository,
        SiteWording wording
    )
    {
        switch (await ImageComponentPicture.ReadAsync(ChosenImageSource, uploadedImageRepository, workRepository))
        {
            case ImageComponentPicture.Missing missing:
                AddServerError(nameof(ImageStorageKey), MissingImageText.Of(missing.Reason, wording));
                return false;
            case ImageComponentPicture.Shown { IsWorksMainImage: true }:
                ImageStorageKey = null;
                break;
        }

        return true;
    }
}
