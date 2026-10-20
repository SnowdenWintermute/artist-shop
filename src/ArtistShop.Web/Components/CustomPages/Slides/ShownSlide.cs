namespace ArtistShop.Web.Components.CustomPages.Slides;

using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// A slide as a visitor sees it: its picture and alt text, and for a work's image the work's title and page
public record ShownSlide(WorkImage Image, string AltText, string? WorkTitle, string? WorkPageUrl)
{
    // a few screens' worth of a collection, which a carousel or slideshow can't usefully go past
    public const int MaximumFromCollection = 100;

    // the slides a visitor sees, in order: a chosen slide with nothing to show, or a collection deleted since, shows none
    public static async Task<IReadOnlyList<ShownSlide>> ReadAsync(
        SlideSource source,
        SlideOwner owner,
        SlideRepository slideRepository,
        CollectionRepository collectionRepository,
        WorkRepository workRepository
    )
    {
        switch (source)
        {
            case SlideSource.ChosenSlides:
                return [.. (await slideRepository.GetAsync(owner)).Select(Of).OfType<ShownSlide>()];
            case SlideSource.WorksInCollection inCollection:
                if (await collectionRepository.GetByIdAsync(inCollection.CollectionId) is not { } collection)
                {
                    return [];
                }

                var works = await workRepository.GetListAsync(
                    WorkListFilter.VisibleWorksIn(collection.Id, pageNumber: 1),
                    searchMatches: null,
                    exclusion: null,
                    MaximumFromCollection
                );

                return
                [
                    .. works.Items.Select(work =>
                        work.PrimaryImage is { } image
                            ? new ShownSlide(image, work.Name.Value, work.Name.Value,
                                WorkPageQuery.Url(work.Slug.Value, collection.Slug.Value, imageNumber: null))
                            : null
                    ).OfType<ShownSlide>(),
                ];
            default:
                throw new InvalidOperationException($"A {source.Type} source can't be read.");
        }
    }

    private static ShownSlide? Of(ChosenSlide slide) =>
        ImageComponentPicture.Of(slide) switch
        {
            ImageComponentPicture.Shown { ArtworkPageUrl: { } pageUrl } shown => new(shown.Image, shown.AltText, shown.Name, pageUrl),
            ImageComponentPicture.Shown shown => new(shown.Image, shown.AltText, WorkTitle: null, WorkPageUrl: null),
            _ => null,
        };
}
