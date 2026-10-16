namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// why an image component with a source shows nothing; its settings page says so
public enum MissingImageReason
{
    LibraryImageDeleted = 1,
    WorkDeleted = 2,
    WorkHasNoImages = 3,
    ImageTakenOffWork = 4,
}

// What an image component's source comes to now: the picture, or why there's none. Name is what the
// settings page calls it; Work is the work it's from, for its page
public abstract record ImageComponentPicture
{
    public sealed record Shown(WorkImage Image, string AltText, string Name, Work? Work) : ImageComponentPicture;

    public sealed record Missing(MissingImageReason Reason) : ImageComponentPicture;

    // null when no image is chosen
    public static async Task<ImageComponentPicture?> ReadAsync(
        ImageSource? source,
        UploadedImageRepository uploadedImageRepository,
        WorkRepository workRepository
    )
    {
        switch (source)
        {
            case null:
                return null;
            case ImageSource.Library library:
                var images = await uploadedImageRepository.GetByStorageKeysAsync([library.StorageKey]);
                return images.GetValueOrDefault(library.StorageKey) is { } image
                    ? new Shown(image.TileImage, image.AltText, image.Name, null)
                    : new Missing(MissingImageReason.LibraryImageDeleted);
            case ImageSource.Work workSource:
                var work = await workRepository.GetByIdAsync(workSource.WorkId);
                return work is null ? new Missing(MissingImageReason.WorkDeleted)
                    : workSource.ImageOf(work) is { } workImage ? new Shown(workImage, work.Name.Value, work.Name.Value, work)
                    : new Missing(work.Images.Count == 0 ? MissingImageReason.WorkHasNoImages : MissingImageReason.ImageTakenOffWork);
            default:
                throw new InvalidOperationException($"An image's {source.Type} source can't be read.");
        }
    }
}
