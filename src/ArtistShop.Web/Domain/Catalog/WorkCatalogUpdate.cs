namespace ArtistShop.Web.Domain.Catalog;

// No type, because a work's type never changes, and no products until the edit form posts them
public record WorkDetailsUpdate(
    WorkId Id,
    WorkName Name,
    WorkSlug CandidateSlug,
    string? Description,
    PartialDate? DateCreated,
    DimensionsCentimeters? Dimensions,
    TimeSpan? Duration,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    IReadOnlyList<CollectionId> CollectionIds
);

// Images are the whole list the form holds, not a change to it
public record WorkCatalogUpdate(
    WorkDetailsUpdate Details,
    IReadOnlyList<WorkImage> Images,
    int MainImageIndex
);
