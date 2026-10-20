namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// how a carousel's or slideshow's "Shows" choices read, in the website's words
public static class SlideSourceLabels
{
    public static string Of(SlideSourceType source, SiteWording wording) =>
        source switch
        {
            SlideSourceType.WorksInCollection => $"{wording.Work.PluralHeading} in one {wording.Collection.SingularInSentence}",
            SlideSourceType.ChosenSlides => "Chosen slides",
            _ => throw new InvalidOperationException($"{source} has no label."),
        };
}
