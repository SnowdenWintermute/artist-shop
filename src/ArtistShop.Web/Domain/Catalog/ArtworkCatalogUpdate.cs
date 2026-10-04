namespace ArtistShop.Web.Domain.Catalog;

// No type, because an artwork's type never changes, and no products until the edit form posts them
public record ArtworkDetailsUpdate(
    ArtworkId Id,
    ArtworkName Name,
    ArtworkSlug CandidateSlug,
    string? Description,
    PartialDate? DateCreated,
    DimensionsCentimeters? Dimensions,
    TimeSpan? Duration,
    IReadOnlyList<VocabularyTermId> VocabularyTermIds,
    IReadOnlyList<SeriesId> SeriesIds
);

// Images are the whole list the form holds, not a change to it
public record ArtworkCatalogUpdate(
    ArtworkDetailsUpdate Details,
    IReadOnlyList<ArtworkImage> Images,
    int MainImageIndex
);
