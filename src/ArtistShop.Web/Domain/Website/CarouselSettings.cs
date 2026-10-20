namespace ArtistShop.Web.Domain.Website;

// The ids of these are only for ordering; settings store the names
public enum CarouselTransition
{
    Fade = 1,
    Slide = 2,
}

public enum CarouselAutoplay
{
    Off = 1,
    ThreeSeconds = 2,
    FiveSeconds = 3,
    EightSeconds = 4,
}

public enum CarouselControls
{
    Arrows = 1,
    Dots = 2,
    Both = 3,
    None = 4,
}

// what clicking a slide does; a library image has no page, so its slide does nothing either way
public enum CarouselLink
{
    ArtworkPage = 1,
    None = 2,
}

public enum CarouselCaption
{
    None = 1,
    // a work's title; a library image has none
    Title = 2,
}

// Slides one at a time, each fitted to the carousel's height, which is never the image's own, since slides of
// different shapes would move the page as they change
public sealed record CarouselSettings(
    SlideSource Source,
    CarouselTransition Transition,
    CarouselAutoplay Autoplay,
    ImageHeight Height,
    ImageFit Fit,
    CarouselControls Controls,
    CarouselLink Link,
    CarouselCaption Caption
) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Carousel;

    public static readonly IReadOnlyList<ImageHeight> Heights = [ImageHeight.Small, ImageHeight.Medium, ImageHeight.Large, ImageHeight.Fill];

    public static readonly CarouselSettings New = new(
        new SlideSource.ChosenSlides(),
        CarouselTransition.Fade,
        CarouselAutoplay.Off,
        ImageHeight.Medium,
        ImageFit.Cover,
        CarouselControls.Both,
        CarouselLink.ArtworkPage,
        CarouselCaption.None
    );

    public static TimeSpan? IntervalOf(CarouselAutoplay autoplay) =>
        autoplay switch
        {
            CarouselAutoplay.Off => null,
            CarouselAutoplay.ThreeSeconds => TimeSpan.FromSeconds(3),
            CarouselAutoplay.FiveSeconds => TimeSpan.FromSeconds(5),
            CarouselAutoplay.EightSeconds => TimeSpan.FromSeconds(8),
            _ => throw new InvalidOperationException($"{autoplay} has no interval."),
        };
}
