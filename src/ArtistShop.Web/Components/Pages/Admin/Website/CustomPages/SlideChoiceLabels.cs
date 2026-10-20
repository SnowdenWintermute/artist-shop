namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using System.Globalization;
using ArtistShop.Web.Domain.Website;

// how a carousel's or slideshow's choices read, in the website's words
public static class SlideChoiceLabels
{
    public static string Of(SlideSourceType source, SiteWording wording) =>
        source switch
        {
            SlideSourceType.WorksInCollection => $"{wording.Work.PluralHeading} in one {wording.Collection.SingularInSentence}",
            SlideSourceType.ChosenSlides => "Chosen slides",
            _ => throw new InvalidOperationException($"{source} has no label."),
        };

    public static string Of(CarouselAutoplay autoplay) =>
        CarouselSettings.IntervalOf(autoplay) is { } interval ? Every(interval) : "Off";

    public static string Of(SlideshowInterval interval) => Every(RowBackground.Slideshow.IntervalOf(interval));

    private static string Every(TimeSpan interval) =>
        $"Every {interval.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds";
}
