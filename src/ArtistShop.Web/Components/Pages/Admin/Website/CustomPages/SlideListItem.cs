namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// A chosen slide as its list shows it: what it's called, or why it shows nothing. Flat, since an island's
// parameters travel as JSON, which can't rebuild a ChosenSlide's abstract types
public record SlideListItem(SlideId Id, string Name, WorkImage? Image, bool IsMissing)
{
    public static SlideListItem From(ChosenSlide slide, SiteWording wording) =>
        ImageComponentPicture.Of(slide) switch
        {
            ImageComponentPicture.Shown shown => new(slide.Id, shown.Name, shown.Image, IsMissing: false),
            ImageComponentPicture.Missing missing => new(slide.Id, MissingImageText.Of(missing.Reason, wording), Image: null, IsMissing: true),
            _ => throw new InvalidOperationException("A slide's picture is neither shown nor missing."),
        };
}
