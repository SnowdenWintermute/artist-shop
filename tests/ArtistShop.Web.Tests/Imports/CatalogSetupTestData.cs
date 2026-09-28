using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// a website's setup for the type and vocabulary import tests: the four fields as the database seeds
// them, a Painting type and a Medium vocabulary for paintings
public static class CatalogSetupTestData
{
    public static readonly ArtworkFieldDefinition[] Fields =
    [
        new(ArtworkField.DateCreated, "Date created", ArtworkField.DateCreated),
        new(ArtworkField.HeightAndWidth, "Height and width", ArtworkField.HeightAndWidth),
        new(ArtworkField.Depth, "Depth", ArtworkField.HeightAndWidth),
        new(ArtworkField.Duration, "Duration", ArtworkField.Duration),
    ];

    public static readonly ArtworkTypeWithFields Painting = new(
        new ArtworkTypeId(1),
        new ArtworkTypeName("Painting"),
        [ArtworkField.DateCreated, ArtworkField.HeightAndWidth]
    );

    public static readonly ArtworkTypeWithFields Sculpture = new(
        new ArtworkTypeId(2),
        new ArtworkTypeName("Sculpture"),
        [ArtworkField.HeightAndWidth, ArtworkField.Depth]
    );

    public static readonly VocabularyId MediumId = new(10);

    public static readonly VocabularySetup Medium = new(
        MediumId,
        new VocabularyName("Medium"),
        [Painting.Id],
        [new VocabularyTerm(new VocabularyTermId(11), new VocabularyTermName("Oil"), MediumId, new VocabularyName("Medium"))]
    );

    public static CatalogSetupSnapshot Snapshot() => new(Fields, [Painting, Sculpture], [Medium]);

    public static CatalogSetupSnapshot EmptySnapshot() => new(Fields, [], []);
}
