namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// The Pages list's two lists: the nav in order, and the other pages by name. Lists, not read-only ones, so
// persisted component state can rebuild them from JSON
public record PageNavLists(List<CustomPage> Nav, List<CustomPage> Others);
