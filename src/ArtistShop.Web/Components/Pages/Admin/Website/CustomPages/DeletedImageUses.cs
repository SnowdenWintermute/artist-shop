using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// What deleting works or library images takes with it on custom pages, read as the confirm opens: the
// image components showing them, deleted, and the pages with a row background showing them, cleared
public record DeletedImageUses(IReadOnlyList<CustomComponentOnPages> ImageComponents, IReadOnlyList<string> RowBackgroundPages)
{
    public static readonly DeletedImageUses None = new([], []);

    public bool IsEmpty => ImageComponents.Count == 0 && RowBackgroundPages.Count == 0;

    public static async Task<DeletedImageUses> OfWorksAsync(
        CustomComponentRepository customComponentRepository,
        PageRowRepository pageRowRepository,
        IReadOnlyCollection<WorkId> ids
    ) =>
        new(
            await customComponentRepository.GetImageComponentsShowingWorksAsync(ids),
            await pageRowRepository.GetPagesWithBackgroundsShowingWorksAsync(ids)
        );

    public static async Task<DeletedImageUses> OfImagesAsync(
        CustomComponentRepository customComponentRepository,
        PageRowRepository pageRowRepository,
        IReadOnlyCollection<string> storageKeys
    ) =>
        new(
            await customComponentRepository.GetImageComponentsShowingImagesAsync(storageKeys),
            await pageRowRepository.GetPagesWithBackgroundsShowingImagesAsync(storageKeys)
        );
}
