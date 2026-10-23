using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// What deleting works or library images takes with it on custom pages, read as the confirm opens: the
// image components showing them, deleted, the pages with a row background showing them, cleared, the
// carousels and slideshows losing slides, and the components linking to works, deleted or losing the link
public record DeletedImageUses(
    IReadOnlyList<CustomComponentOnPages> ImageComponents,
    IReadOnlyList<string> RowBackgroundPages,
    SlideOwnersShowing Slides,
    LinkingComponents Linking
)
{
    public static readonly DeletedImageUses None = new([], [], new SlideOwnersShowing([], []), LinkingComponents.None);

    public bool IsEmpty =>
        ImageComponents.Count == 0 && RowBackgroundPages.Count == 0 && Slides.Carousels.Count == 0 && Slides.SlideshowPages.Count == 0
        && Linking.IsEmpty;

    public static async Task<DeletedImageUses> OfWorksAsync(
        CustomComponentRepository customComponentRepository,
        PageRowRepository pageRowRepository,
        SlideRepository slideRepository,
        IReadOnlyCollection<WorkId> ids
    ) =>
        new(
            await customComponentRepository.GetImageComponentsShowingWorksAsync(ids),
            await pageRowRepository.GetPagesWithBackgroundsShowingWorksAsync(ids),
            await slideRepository.GetOwnersShowingWorksAsync(ids),
            await customComponentRepository.GetLinkingToWorksAsync(ids)
        );

    public static async Task<DeletedImageUses> OfImagesAsync(
        CustomComponentRepository customComponentRepository,
        PageRowRepository pageRowRepository,
        SlideRepository slideRepository,
        IReadOnlyCollection<string> storageKeys
    ) =>
        new(
            await customComponentRepository.GetImageComponentsShowingImagesAsync(storageKeys),
            await pageRowRepository.GetPagesWithBackgroundsShowingImagesAsync(storageKeys),
            await slideRepository.GetOwnersShowingImagesAsync(storageKeys),
            LinkingComponents.None
        );
}
