using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// the pages a component is on, as the admin pages say it
public static class CustomComponentPagesText
{
    public static string WhereItIs(CustomComponentInUse component) =>
        component.PageNames.Count == 0 ? "Not on any page." : $"On {PageNamesText.Of(component.PageNames)}.";

    public static string DeleteWarning(CustomComponentInUse component) =>
        component.PageNames.Count == 0
            ? "It isn't on any page."
            : $"It will be taken off {PageNamesText.Of(component.PageNames)}.";
}
