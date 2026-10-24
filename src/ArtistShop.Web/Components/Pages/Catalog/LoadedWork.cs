namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all. Mentions are the published posts that embed it, newest
// first; PageMentions the custom pages with a text component that embeds it, in no order.
// CollectionsPage is read only when the walk has no collection, as the Up link goes there instead
public record LoadedWork(
    Work Work,
    WorkWalk Walk,
    IReadOnlyList<PostMention> Mentions,
    IReadOnlyList<CustomPage> PageMentions,
    CustomPage? CollectionsPage,
    DimensionDisplay DimensionDisplay
);
