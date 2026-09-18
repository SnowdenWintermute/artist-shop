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
    // series the artwork joins that don't exist yet, created as it is added
    IReadOnlyList<SeriesName> NewSeriesNames,
    IReadOnlyList<ProductAddition> Products
);
