using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkTypes;

public record LoadedArtworkType(ArtworkTypeWithFields ArtworkType, ArtworkTypeArtworkCounts ArtworkCounts);
