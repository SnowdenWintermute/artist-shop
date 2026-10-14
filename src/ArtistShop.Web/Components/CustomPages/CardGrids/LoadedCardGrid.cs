namespace ArtistShop.Web.Components.CustomPages.CardGrids;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;

// WorksCollection is the collection the works are from, for their links; null for any other source
public record LoadedCardGrid(string? Heading, CardGridPage Page, CollectionSlug? WorksCollection);
