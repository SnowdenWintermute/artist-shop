namespace ArtistShop.Web.Domain.Catalog;

using ArtistShop.Web.Domain.Commerce;

public record WorkCatalogAddition(
    WorkTypeId TypeId,
    WorkName Name,
    WorkSlug CandidateSlug,
    string? Description,
    PartialDate? DateCreated,
    DimensionsCentimeters? Dimensions,
    TimeSpan? Duration,
    IReadOnlyList<WorkImage> Images,
    int MainImageIndex,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    IReadOnlyList<CollectionId> CollectionIds,
    // collections the work joins that don't exist yet, created as it is added
    IReadOnlyList<CollectionName> NewCollectionNames,
    IReadOnlyList<ProductAddition> Products
);
