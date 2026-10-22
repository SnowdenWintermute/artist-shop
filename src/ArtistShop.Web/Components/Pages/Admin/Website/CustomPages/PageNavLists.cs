namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// The Pages list's two lists: the nav in order, and the other pages by name. HoldingPostLists are the pages that
// can be the blog page, and HoldingAllCollectionsGrids the ones that can be the collections page. Lists, not
// read-only ones, so persisted component state can rebuild them from JSON
public record PageNavLists(
    List<CustomPage> Nav,
    List<CustomPage> Others,
    List<CustomPageId> HoldingPostLists,
    List<CustomPageId> HoldingAllCollectionsGrids
);
