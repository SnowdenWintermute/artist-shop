namespace ArtistShop.Web.Domain.Catalog;

using ArtistShop.Web.Domain.Commerce;

public record ArtworkCatalogAddition(
    ArtworkTypeId TypeId,
    ArtworkName Name,
    ArtworkSlug CandidateSlug,
    string? Description,
    PartialDate? DateCreated,
    DimensionsCentimeters? Dimensions,
    TimeSpan? Duration,
    IReadOnlyList<ArtworkImage> Images,
    int MainImageIndex,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    IReadOnlyList<SeriesId> SeriesIds,
    IReadOnlyList<ProductAddition> Products
);
