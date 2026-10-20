namespace ArtistShop.Web.Domain.Website;

using ArtistShop.Web.Domain.Catalog;

public record SlideId(int Value);

// what holds a chosen list of slides: a carousel, or a row's slideshow background
public abstract record SlideOwner
{
    public sealed record Component(CustomComponentId Id) : SlideOwner;

    public sealed record Row(PageRowId Id) : SlideOwner;
}

// One slide of a chosen list: its source, and the image it shows now. Image is null for a work whose picked
// image was taken off it, or that has none
public record ChosenSlide(SlideId Id, ImageSource Source, SlideImage? Image);

public abstract record SlideImage(WorkImage Image)
{
    // Name is what the image library calls it
    public sealed record OfLibrary(WorkImage Image, string AltText, string Name) : SlideImage(Image);

    // Place is the image's among the work's, for the work's page
    public sealed record OfWork(WorkImage Image, WorkName WorkName, WorkSlug WorkSlug, int Place, bool IsMain) : SlideImage(Image);
}

// What deleting works or library images takes slides out of: the carousels, with the pages they're on, and the
// pages with a row slideshow
public record SlideOwnersShowing(IReadOnlyList<CustomComponentOnPages> Carousels, IReadOnlyList<string> SlideshowPages);

// the ids are only for ordering; settings store the names
public enum SlideSourceType
{
    WorksInCollection = 1,
    ChosenSlides = 2,
}

// What a carousel or a slideshow background shows
public abstract record SlideSource
{
    public abstract SlideSourceType Type { get; }

    // collectionId is only for WorksInCollection, which needs it
    public static SlideSource Of(SlideSourceType type, CollectionId? collectionId) =>
        type switch
        {
            SlideSourceType.WorksInCollection => new WorksInCollection(
                collectionId ?? throw new InvalidOperationException("Works in a collection need the collection.")
            ),
            SlideSourceType.ChosenSlides => new ChosenSlides(),
            _ => throw new InvalidOperationException($"{type} isn't a slide source."),
        };

    // each of the collection's works a visitor sees, in the artist's order, by its main image
    public sealed record WorksInCollection(CollectionId CollectionId) : SlideSource
    {
        public override SlideSourceType Type => SlideSourceType.WorksInCollection;
    }

    // the owner's chosen list, kept in slides
    public sealed record ChosenSlides : SlideSource
    {
        public override SlideSourceType Type => SlideSourceType.ChosenSlides;
    }
}
