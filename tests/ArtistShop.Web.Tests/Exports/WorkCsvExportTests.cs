using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class WorkCsvExportTests
{
    private static readonly VocabularyId MediumId = new(1);
    private static readonly VocabularyName MediumName = new("Medium");
    private static readonly VocabularyTerm Oil = new(new VocabularyTermId(11), new VocabularyTermName("Oil"), MediumId, MediumName);
    private static readonly VocabularyTerm Bronze = new(new VocabularyTermId(12), new VocabularyTermName("Bronze"), MediumId, MediumName);
    private static readonly VocabularyWithTerms Medium = new(MediumId, MediumName, IsMutuallyExclusive: false, [Oil, Bronze]);
    private static readonly VocabularySetup MediumSetup = new(MediumId, MediumName, IsMutuallyExclusive: false, [new WorkTypeId(1)], [Oil, Bronze]);
    private static readonly Collection SunriseSunset = new(new CollectionId(21), new CollectionName("Sunrise, Sunset"), new CollectionSlug("sunrise-sunset"));
    private static readonly Collection Gardens = new(new CollectionId(22), new CollectionName("Gardens"), new CollectionSlug("gardens"));
    private static readonly ProductType Original = new(new ProductTypeId(31), new ProductTypeName("Original"), IsDefault: true);
    private static readonly ProductType Print = new(new ProductTypeId(32), new ProductTypeName("Print"), IsDefault: false);

    private static readonly WorkTypeWithFields Sculpture = new(
        new WorkTypeId(1),
        new WorkTypeName("Sculpture"),
        [WorkField.DateCreated, WorkField.HeightAndWidth, WorkField.Depth, WorkField.Duration]
    );

    private static Work MakeWork(
        string name,
        string? description = null,
        PartialDate? dateCreated = null,
        DimensionsCentimeters? dimensions = null,
        TimeSpan? duration = null,
        IReadOnlyList<Collection>? collections = null,
        IReadOnlyList<VocabularyTerm>? terms = null,
        IReadOnlyList<Product>? products = null
    ) =>
        new(
            new WorkId(Random.Shared.Next()),
            new WorkType(Sculpture.Id, Sculpture.Name),
            new WorkName(name),
            WorkSlug.FromName(name),
            description,
            dateCreated,
            dimensions,
            duration,
            images: [],
            collections ?? [],
            terms ?? [],
            products ?? []
        );

    private static WorkImportPlan Import(string csv, char listSeparator) =>
        WorkImportPlanner.Plan(
            csv,
            new WorkImportSettings(LengthUnit.Centimeters, Original.Id, IsOneOfAKind: true, listSeparator),
            new WorkImportCatalogSnapshot(
                Sculpture,
                [Medium],
                [new Vocabulary(MediumId, MediumName)],
                [SunriseSunset, Gardens],
                [Original, Print],
                TypeWorks: []
            )
        );

    // what the import makes from the file is what was exported
    [Fact]
    public void TheImportReadsBackWhatWasExported()
    {
        var work = MakeWork(
            "Standing figure",
            description: "Cast in two parts, \"joined\" later.\nSigned on the base.",
            dateCreated: new PartialDate(new DateOnly(2021, 3, 1), DatePrecision.Month),
            // inches imported earlier leave four decimal places
            dimensions: new DimensionsCentimeters(new Dimensions(60.96m, 45.72m, 10.1234m)),
            duration: TimeSpan.FromSeconds(3725),
            collections: [SunriseSunset, Gardens],
            terms: [Oil, Bronze]
        );

        var plan = Import(WorkCsvExport.ForType(Sculpture, [MediumSetup], [work], ';'), ';');

        Assert.Empty(plan.Errors);
        var addition = Assert.Single(plan.Additions).Addition;
        Assert.Equal(work.Name, addition.Name);
        Assert.Equal(work.Description, addition.Description);
        Assert.Equal(work.DateCreated, addition.DateCreated);
        Assert.Equal(work.Dimensions, addition.Dimensions);
        Assert.Equal(work.Duration, addition.Duration);
        Assert.Equal([SunriseSunset.Id, Gardens.Id], addition.CollectionIds);
        Assert.Equal([Oil.Id, Bronze.Id], addition.VocabularyTermIds);
        Assert.Empty(addition.Products);
    }

    // the import refuses a column for a field the type doesn't have
    [Fact]
    public void OnlyTheTypesFieldsAreColumns()
    {
        var screenshot = new WorkTypeWithFields(new WorkTypeId(2), new WorkTypeName("Screenshot"), [WorkField.DateCreated]);

        var csv = WorkCsvExport.ForType(screenshot, [MediumSetup], [], ';');

        Assert.Equal("title,slug,description,dateCreated,collections,Medium\r\n", csv);
    }

    [Fact]
    public void AVocabularyNamedLikeAColumnIsPrefixed()
    {
        var screenshot = new WorkTypeWithFields(new WorkTypeId(2), new WorkTypeName("Screenshot"), []);
        var collectionVocabulary = new VocabularySetup(new VocabularyId(4), new VocabularyName("Collections"), IsMutuallyExclusive: false, [screenshot.Id], []);

        var csv = WorkCsvExport.ForType(screenshot, [collectionVocabulary], [], ';');

        Assert.Equal("title,slug,description,collections,vocabulary:Collections\r\n", csv);
    }

    [Fact]
    public void TheListSeparatorIsOneNoNameContains()
    {
        var separator = WorkCsvExport.ChooseListSeparator(["Night; Day", "Ink | wash", "Plain"]);

        Assert.Equal('/', separator);
    }

    [Fact]
    public void EachProductIsARow()
    {
        var product = new Product(new ProductId(1), Print, "Large", 120.5m, EditionSize: null, Stock: 3);

        var csv = WorkCsvExport.Products([MakeWork("Dawn", products: [product])]);

        Assert.Equal(
            "workType,title,slug,productType,label,price,editionSize,stock\r\nSculpture,Dawn,dawn,Print,Large,120.50,,3\r\n",
            csv
        );
    }
}
