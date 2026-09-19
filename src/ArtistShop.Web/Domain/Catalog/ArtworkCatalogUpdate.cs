namespace ArtistShop.Web.Domain.Catalog;

// No type, because an artwork's type never changes. No images or products either: they stay as they
// are until the edit page's second phase can post them
public record ArtworkCatalogUpdate(
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
