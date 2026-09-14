namespace ArtistShop.Web.Domain.Catalog;

using ArtistShop.Web.Domain.Commerce;

public record PaintingCatalogAddition(
    ShopItemName Name,
    ShopItemSlug CandidateSlug,
    decimal? Price,
    int Stock,
    PartialDate? DatePainted,
    string? Description,
    DimensionsCentimeters? Dimensions,
    IReadOnlyList<ShopItemImage> Images,
    int MainImageIndex,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    IReadOnlyList<SeriesId> SeriesIds
);
