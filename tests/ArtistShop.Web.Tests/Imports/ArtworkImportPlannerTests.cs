using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class ArtworkImportPlannerTests
{
    private static readonly VocabularyId MediumId = new(1);
    private static readonly VocabularyName MediumName = new("Medium");
    private static readonly VocabularyTerm Oil = new(new VocabularyTermId(11), new VocabularyTermName("Oil"), MediumId, MediumName);
    private static readonly VocabularyTerm Acrylic = new(new VocabularyTermId(12), new VocabularyTermName("Acrylic"), MediumId, MediumName);
    private static readonly Vocabulary Glaze = new(new VocabularyId(2), new VocabularyName("Glaze"));
    private static readonly Series SunriseSunset = new(new SeriesId(21), new SeriesName("Sunrise, Sunset"), new SeriesSlug("sunrise-sunset"));
    private static readonly Series Gardens = new(new SeriesId(22), new SeriesName("Gardens"), new SeriesSlug("gardens"));
    private static readonly ProductType Original = new(new ProductTypeId(31), new ProductTypeName("Original"));
    private static readonly ProductType Print = new(new ProductTypeId(32), new ProductTypeName("Print"));

    private static readonly ArtworkField[] PaintingFields = [ArtworkField.DateCreated, ArtworkField.HeightAndWidth];

    private static readonly ArtworkImportSettings OneOfAKindInInches = new(LengthUnit.Inches, Original.Id, IsOneOfAKind: true, ListSeparator: ';');
    private static readonly ArtworkImportSettings EditionsInCentimeters = new(LengthUnit.Centimeters, Print.Id, IsOneOfAKind: false, ListSeparator: ';');

    // Glaze exists but only applies to other types
    private static ArtworkImportCatalogSnapshot Snapshot(IReadOnlyList<ArtworkField> fields) =>
        new(
            new ArtworkTypeWithFields(new ArtworkTypeId(1), new ArtworkTypeName("Painting"), fields),
            [new VocabularyWithTerms(MediumId, MediumName, [Oil, Acrylic])],
            [new Vocabulary(MediumId, MediumName), Glaze],
            [SunriseSunset, Gardens],
            [Original, Print],
            ["Existing Painting"]
        );

    private static ArtworkImportPlan PlanPaintings(string csv) =>
        ArtworkImportPlanner.Plan(csv, OneOfAKindInInches, Snapshot(PaintingFields));

    private static void AssertError(ArtworkImportPlan plan, int rowNumber, string? column)
    {
        Assert.Contains(plan.Errors, error => error.RowNumber == rowNumber && error.Column == column);
        Assert.False(plan.CanImport);
    }

    [Fact]
    public void PlansAFullRow()
    {
        var plan = PlanPaintings(
            """
            title,description,dateCreated,height,width,medium,series,price
            Dawn,Early light,2019-05,8.5,10,oil; Acrylic,"Sunrise, Sunset;gardens",400
            """
        );

        Assert.Empty(plan.Errors);
        Assert.True(plan.CanImport);
        var (rowNumber, addition) = Assert.Single(plan.Additions);
        Assert.Equal(2, rowNumber);
        Assert.Equal("Dawn", addition.Name.Value);
        Assert.Equal("dawn", addition.CandidateSlug.Value);
        Assert.Equal("Early light", addition.Description);
        Assert.Equal(new PartialDate(new DateOnly(2019, 5, 1), DatePrecision.Month), addition.DateCreated);
        Assert.Equal(new DimensionsCentimeters(new Dimensions(21.59m, 25.4m, depth: null)), addition.Dimensions);
        Assert.Equal([Oil.Id, Acrylic.Id], addition.VocabularyTermIds);
        Assert.Equal([SunriseSunset.Id, Gardens.Id], addition.SeriesIds);
        var product = Assert.Single(addition.Products);
        Assert.Equal(new ProductAddition(Original.Id, Label: null, 400m, EditionSize: 1, Stock: 1), product);
    }

    [Fact]
    public void ASoldRowHasNoStockAndMayHaveNoPrice()
    {
        var plan = PlanPaintings(
            """
            title,price,sold
            Dawn,,TRUE
            Dusk,250,true
            """
        );

        Assert.Equal(
            [
                new ProductAddition(Original.Id, Label: null, Price: null, EditionSize: 1, Stock: 0),
                new ProductAddition(Original.Id, Label: null, 250m, EditionSize: 1, Stock: 0),
            ],
            plan.Additions.Select(addition => Assert.Single(addition.Addition.Products))
        );
    }

    [Fact]
    public void ARowWithNoPriceThatIsNotSoldHasNoProduct()
    {
        var plan = PlanPaintings(
            """
            title,price,sold
            Dawn,,FALSE
            """
        );

        Assert.Empty(Assert.Single(plan.Additions).Addition.Products);
    }

    [Fact]
    public void YearZeroMeansNoDate()
    {
        var plan = PlanPaintings(
            """
            title,dateCreated
            Dawn,0
            """
        );

        Assert.Null(Assert.Single(plan.Additions).Addition.DateCreated);
    }

    [Fact]
    public void SkipsTitlesAlreadyInTheCatalog()
    {
        var plan = PlanPaintings(
            """
            title,price
            existing painting,not a price
            Dawn,100
            """
        );

        Assert.Empty(plan.Errors);
        Assert.Equal(
            new ArtworkImportSkippedRow(2, "existing painting", ArtworkImportSkipReason.AlreadyInCatalog),
            Assert.Single(plan.SkippedRows)
        );
        Assert.Equal("Dawn", Assert.Single(plan.Additions).Addition.Name.Value);
    }

    [Fact]
    public void SkipsEveryRowOfARepeatedTitle()
    {
        var plan = PlanPaintings(
            """
            title,height,width
            Dawn,8,10
            Dusk,8,10
            DAWN,10,8
            """
        );

        Assert.Equal([2, 4], plan.SkippedRows.Select(row => row.RowNumber));
        Assert.All(plan.SkippedRows, row => Assert.Equal(ArtworkImportSkipReason.TitleRepeatedInFile, row.Reason));
        Assert.Equal("Dusk", Assert.Single(plan.Additions).Addition.Name.Value);
    }

    [Fact]
    public void ReportsHeaderProblemsWithoutReadingRows()
    {
        var plan = PlanPaintings(
            """
            title,year,glaze,title,duration
            ,not checked,,,
            """
        );

        AssertError(plan, 1, "year");
        AssertError(plan, 1, "glaze");
        AssertError(plan, 1, "title");
        AssertError(plan, 1, "duration");
        Assert.All(plan.Errors, error => Assert.Equal(1, error.RowNumber));
        Assert.Empty(plan.Additions);
    }

    [Fact]
    public void NeedsATitleColumn()
    {
        AssertError(PlanPaintings("price\n100\n"), 1, null);
    }

    [Fact]
    public void NeedsHeightAndWidthColumnsTogether()
    {
        AssertError(PlanPaintings("title,height\nDawn,8\n"), 1, null);
    }

    [Fact]
    public void IgnoresAnEmptyColumnWithNoHeader()
    {
        var plan = PlanPaintings("title,,\nDawn,,\n");

        Assert.True(plan.CanImport);
    }

    [Fact]
    public void RejectsValuesInAColumnWithNoHeader()
    {
        AssertError(PlanPaintings("title,\nDawn,oops\n"), 1, null);
    }

    [Fact]
    public void ReportsEveryBadCellWithItsRowAndColumn()
    {
        var plan = PlanPaintings(
            """
            title,dateCreated,height,width,medium,series,price,sold
            ,,,,,,,
            Dawn,5/1/2019,8.125,10,Tempera,Nocturnes,$400,maybe
            Dusk,,8,,,,,
            !!!,,,,,,,
            """
        );

        AssertError(plan, 3, "dateCreated");
        AssertError(plan, 3, "height");
        AssertError(plan, 3, "Medium");
        AssertError(plan, 3, "series");
        AssertError(plan, 3, "price");
        AssertError(plan, 3, "sold");
        AssertError(plan, 4, "width");
        AssertError(plan, 5, "title");
        Assert.Empty(plan.Additions);
    }

    [Fact]
    public void AllowsFourDecimalPlacesInCentimetres()
    {
        var plan = ArtworkImportPlanner.Plan(
            "title,height,width,editionSize,stock\nDawn,20.3125,30,,\n",
            EditionsInCentimeters,
            Snapshot(PaintingFields)
        );

        Assert.Equal(20.3125m, Assert.Single(plan.Additions).Addition.Dimensions?.Height);
    }

    [Fact]
    public void RejectsALengthOutsideTheLimits()
    {
        AssertError(PlanPaintings("title,height,width\nDawn,4000,10\n"), 2, "height");
    }

    [Fact]
    public void ReadsDepthAndDurationWhenTheTypeHasThem()
    {
        var plan = ArtworkImportPlanner.Plan(
            """
            title,height,width,depth,duration
            Dance,10,20,5,1:02:03
            Song,,,,75:00
            """,
            OneOfAKindInInches,
            Snapshot([ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration])
        );

        Assert.Empty(plan.Errors);
        Assert.Equal(12.7m, plan.Additions[0].Addition.Dimensions?.Depth);
        Assert.Equal(new TimeSpan(1, 2, 3), plan.Additions[0].Addition.Duration);
        Assert.Equal(TimeSpan.FromMinutes(75), plan.Additions[1].Addition.Duration);
    }

    [Fact]
    public void RejectsABadDurationAndADepthWithoutHeight()
    {
        var plan = ArtworkImportPlanner.Plan(
            """
            title,height,width,depth,duration
            Dance,,,5,1:60
            """,
            OneOfAKindInInches,
            Snapshot([ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration])
        );

        AssertError(plan, 2, "depth");
        AssertError(plan, 2, "duration");
    }

    [Fact]
    public void PlansEditionsFromTheirColumns()
    {
        var plan = ArtworkImportPlanner.Plan(
            """
            title,price,editionSize,stock
            Open print,20,,40
            Limited print,50,10,3
            Sold out print,,10,0
            No products,,,
            """,
            EditionsInCentimeters,
            Snapshot(PaintingFields)
        );

        Assert.Empty(plan.Errors);
        Assert.Equal(
            [
                [new ProductAddition(Print.Id, Label: null, 20m, EditionSize: null, Stock: 40)],
                [new ProductAddition(Print.Id, Label: null, 50m, EditionSize: 10, Stock: 3)],
                [new ProductAddition(Print.Id, Label: null, Price: null, EditionSize: 10, Stock: 0)],
                [],
            ],
            plan.Additions.Select(addition => addition.Addition.Products)
        );
    }

    [Fact]
    public void RejectsEditionRowsThatBreakTheStockRules()
    {
        var plan = ArtworkImportPlanner.Plan(
            """
            title,price,editionSize,stock
            Too many,50,10,11
            No price,,,5
            No stock,50,10,
            """,
            EditionsInCentimeters,
            Snapshot(PaintingFields)
        );

        AssertError(plan, 2, "stock");
        AssertError(plan, 3, "price");
        AssertError(plan, 4, "stock");
    }

    [Fact]
    public void EditionsNeedStockColumnsAndNoSoldColumn()
    {
        var plan = ArtworkImportPlanner.Plan("title,sold\nDawn,TRUE\n", EditionsInCentimeters, Snapshot(PaintingFields));

        AssertError(plan, 1, "sold");
        Assert.Equal(3, plan.Errors.Count);
    }

    [Fact]
    public void OneOfAKindRejectsStockColumns()
    {
        var plan = PlanPaintings("title,editionSize,stock\nDawn,1,1\n");

        AssertError(plan, 1, "editionSize");
        AssertError(plan, 1, "stock");
    }

    [Fact]
    public void RejectsAProductTypeThatNoLongerExists()
    {
        var settings = OneOfAKindInInches with { ProductTypeId = new ProductTypeId(99) };

        AssertError(ArtworkImportPlanner.Plan("title\nDawn\n", settings, Snapshot(PaintingFields)), 1, null);
    }

    [Fact]
    public void ReportsMalformedCsv()
    {
        var plan = PlanPaintings("title\nDawn\n\"Dusk\n");

        AssertError(plan, 3, null);
    }

    [Fact]
    public void FingerprintChangesOnlyWithThePlan()
    {
        const string csv = "title,series\nDawn,Gardens\n";
        var snapshot = Snapshot(PaintingFields);

        var first = ArtworkImportPlanner.Plan(csv, OneOfAKindInInches, snapshot);
        var same = ArtworkImportPlanner.Plan(csv, OneOfAKindInInches, snapshot);
        var afterGardensWasRecreated = ArtworkImportPlanner.Plan(
            csv,
            OneOfAKindInInches,
            snapshot with { AllSeries = [Gardens with { Id = new SeriesId(23) }] }
        );

        Assert.Equal(first.Fingerprint(), same.Fingerprint());
        Assert.NotEqual(first.Fingerprint(), afterGardensWasRecreated.Fingerprint());
    }

    // the artist's old export: one row per series membership, a year column and a catalogue number
    [Fact]
    public void RejectsTheOldPaintingsExport()
    {
        var csv = File.ReadAllText(Path.Combine(RepositoryRoot(), "paintings.csv"));
        var snapshot = Snapshot(PaintingFields) with
        {
            AllVocabularies = [new Vocabulary(MediumId, new VocabularyName("drawingMaterial")), new Vocabulary(new VocabularyId(3), new VocabularyName("support"))],
            TypeVocabularies =
            [
                new VocabularyWithTerms(MediumId, new VocabularyName("drawingMaterial"), []),
                new VocabularyWithTerms(new VocabularyId(3), new VocabularyName("support"), []),
            ],
        };

        var plan = ArtworkImportPlanner.Plan(csv, OneOfAKindInInches, snapshot);

        Assert.Equal(["catalogueNumber", "year"], plan.Errors.Select(error => error.Column).Order());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(directory.FullName, "ArtistShop.slnx")))
        {
            directory = directory.Parent ?? throw new InvalidOperationException("ArtistShop.slnx not found above the test output.");
        }

        return directory.FullName;
    }
}
