namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// why an image component's source shows nothing: its settings page says so, and refuses a picked one
public enum MissingImageReason
{
    LibraryImageDeleted = 1,
    WorkDeleted = 2,
    WorkHasNoImages = 3,
    ImageTakenOffWork = 4,
}

public static class MissingImageText
{
    public static string Of(MissingImageReason reason, SiteWording wording) =>
        reason switch
        {
            MissingImageReason.LibraryImageDeleted => "That image was deleted from the image library.",
            MissingImageReason.WorkDeleted => $"That {wording.Work.SingularInSentence} was deleted.",
            MissingImageReason.WorkHasNoImages => $"That {wording.Work.SingularInSentence} has no images.",
            MissingImageReason.ImageTakenOffWork => $"That image was taken off its {wording.Work.SingularInSentence}.",
            _ => throw new InvalidOperationException($"{reason} has no text."),
        };
}

// What an image component's source comes to now: the picture, or why there's none
public abstract record ImageComponentPicture
{
    // Name is what the settings page calls it. A work's image has its page, opened at the image, and says
    // whether it's the work's main image
    public sealed record Shown(WorkImage Image, string AltText, string Name, string? ArtworkPageUrl, bool IsWorksMainImage)
        : ImageComponentPicture;

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
                    ? new Shown(image.TileImage, image.AltText, image.Name, ArtworkPageUrl: null, IsWorksMainImage: false)
                    : new Missing(MissingImageReason.LibraryImageDeleted);
            case ImageSource.Work workSource:
                var work = await workRepository.GetByIdAsync(workSource.WorkId);
                return work is null ? new Missing(MissingImageReason.WorkDeleted)
                    : workSource.ImageIndexOf(work) is int index ? ShownOf(work, workSource, index)
                    : new Missing(work.Images.Count == 0 ? MissingImageReason.WorkHasNoImages : MissingImageReason.ImageTakenOffWork);
            default:
                throw new InvalidOperationException($"An image's {source.Type} source can't be read.");
        }
    }

    private static Shown ShownOf(Work work, ImageSource.Work source, int index) =>
        new(work.Images[index], work.Name.Value, work.Name.Value, WorkPageUrl(work.Slug, source.StorageKey is null, index),
            index == work.MainImageIndex);

    // A chosen slide's image, read with it. Deleting its image or work deletes the slide, so only a work's image
    // can be missing
    public static ImageComponentPicture Of(ChosenSlide slide) =>
        slide.Image switch
        {
            SlideImage.OfLibrary library => new Shown(library.Image, library.AltText, library.Name, ArtworkPageUrl: null,
                IsWorksMainImage: false),
            SlideImage.OfWork work => new Shown(work.Image, work.WorkName.Value, work.WorkName.Value,
                WorkPageUrl(work.WorkSlug, slide.Source is ImageSource.Work { StorageKey: null }, work.Place), work.IsMain),
            _ => new Missing(slide.Source is ImageSource.Work { StorageKey: not null }
                ? MissingImageReason.ImageTakenOffWork
                : MissingImageReason.WorkHasNoImages),
        };

    // following the main image, the page opens where it would anyway: at the main image
    public static string WorkPageUrl(WorkSlug slug, bool followsMainImage, int index) =>
        followsMainImage
            ? WorkPageQuery.Url(slug.Value, collectionSlug: null, imageNumber: null)
            : WorkPageQuery.ImageUrl(slug.Value, collectionSlug: null, index);
}
