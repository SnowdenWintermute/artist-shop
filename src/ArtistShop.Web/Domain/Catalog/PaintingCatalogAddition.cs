namespace ArtistShop.Web.Domain.Catalog;

using ArtistShop.Web.Domain.Commerce;

public record PaintingCatalogAddition(
    ShopItemName Name,
    ShopItemSlug Slug,
    decimal Price,
    int Stock,
    DateOnly DatePainted,
    string? Description,
    DimensionsCentimeters? Dimensions,
    IReadOnlyList<string> ImageRelativeUrls,
    int MainImageIndex,
    IReadOnlyList<MediumId> MediumIds,
    IReadOnlyList<SupportId> SupportIds,
    IReadOnlyList<SeriesId> SeriesIds
);
