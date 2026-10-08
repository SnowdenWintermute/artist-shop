using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// a website's setup for the type and vocabulary import tests: the four fields as the database seeds
// them, a Painting type and a Medium vocabulary for paintings
public static class CatalogSetupTestData
{
    public static readonly WorkFieldDefinition[] Fields =
    [
        new(WorkField.DateCreated, "Date created", WorkField.DateCreated),
        new(WorkField.HeightAndWidth, "Height and width", WorkField.HeightAndWidth),
        new(WorkField.Depth, "Depth", WorkField.HeightAndWidth),
        new(WorkField.Duration, "Duration", WorkField.Duration),
    ];

    public static readonly WorkTypeWithFields Painting = new(
        new WorkTypeId(1),
        new WorkTypeName("Painting"),
        [WorkField.DateCreated, WorkField.HeightAndWidth]
    );

    public static readonly WorkTypeWithFields Sculpture = new(
        new WorkTypeId(2),
        new WorkTypeName("Sculpture"),
        [WorkField.HeightAndWidth, WorkField.Depth]
    );

    public static readonly VocabularyId MediumId = new(10);

    public static readonly VocabularySetup Medium = new(
        MediumId,
        new VocabularyName("Medium"),
        IsMutuallyExclusive: false,
        [Painting.Id],
        [new VocabularyTerm(new VocabularyTermId(11), new VocabularyTermName("Oil"), MediumId, new VocabularyName("Medium"))]
    );

    public static CatalogSetupSnapshot Snapshot() => new(Fields, [Painting, Sculpture], [Medium]);

    public static CatalogSetupSnapshot EmptySnapshot() => new(Fields, [], []);
}
