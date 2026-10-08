using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class ArtworkCsvExportTests
{
    private static readonly VocabularyId MediumId = new(1);
    private static readonly VocabularyName MediumName = new("Medium");
    private static readonly VocabularyTerm Oil = new(new VocabularyTermId(11), new VocabularyTermName("Oil"), MediumId, MediumName);
    private static readonly VocabularyTerm Bronze = new(new VocabularyTermId(12), new VocabularyTermName("Bronze"), MediumId, MediumName);
    private static readonly VocabularyWithTerms Medium = new(MediumId, MediumName, IsMutuallyExclusive: false, [Oil, Bronze]);
    private static readonly VocabularySetup MediumSetup = new(MediumId, MediumName, IsMutuallyExclusive: false, [new ArtworkTypeId(1)], [Oil, Bronze]);
    private static readonly Series SunriseSunset = new(new SeriesId(21), new SeriesName("Sunrise, Sunset"), new SeriesSlug("sunrise-sunset"));
    private static readonly Series Gardens = new(new SeriesId(22), new SeriesName("Gardens"), new SeriesSlug("gardens"));
    private static readonly ProductType Original = new(new ProductTypeId(31), new ProductTypeName("Original"), IsDefault: true);
    private static readonly ProductType Print = new(new ProductTypeId(32), new ProductTypeName("Print"), IsDefault: false);

    private static readonly ArtworkTypeWithFields Sculpture = new(
        new ArtworkTypeId(1),
        new ArtworkTypeName("Sculpture"),
        [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration]
    );

    private static Artwork MakeArtwork(
        string name,
        string? description = null,
        PartialDate? dateCreated = null,
        DimensionsCentimeters? dimensions = null,
        TimeSpan? duration = null,
        IReadOnlyList<Series>? series = null,
        IReadOnlyList<VocabularyTerm>? terms = null,
        IReadOnlyList<Product>? products = null
    ) =>
        new(
            new ArtworkId(Random.Shared.Next()),
            new ArtworkType(Sculpture.Id, Sculpture.Name),
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            description,
            dateCreated,
            dimensions,
            duration,
            images: [],
            series ?? [],
            terms ?? [],
            products ?? []
        );

    private static ArtworkImportPlan Import(string csv, char listSeparator) =>
        ArtworkImportPlanner.Plan(
            csv,
            new ArtworkImportSettings(LengthUnit.Centimeters, Original.Id, IsOneOfAKind: true, listSeparator),
            new ArtworkImportCatalogSnapshot(
                Sculpture,
                [Medium],
                [new Vocabulary(MediumId, MediumName)],
                [SunriseSunset, Gardens],
                [Original, Print],
                TypeArtworks: []
            )
        );

    // what the import makes from the file is what was exported
    [Fact]
    public void TheImportReadsBackWhatWasExported()
    {
        var artwork = MakeArtwork(
            "Standing figure",
            description: "Cast in two parts, \"joined\" later.\nSigned on the base.",
            dateCreated: new PartialDate(new DateOnly(2021, 3, 1), DatePrecision.Month),
            // inches imported earlier leave four decimal places
            dimensions: new DimensionsCentimeters(new Dimensions(60.96m, 45.72m, 10.1234m)),
            duration: TimeSpan.FromSeconds(3725),
            series: [SunriseSunset, Gardens],
            terms: [Oil, Bronze]
        );

        var plan = Import(ArtworkCsvExport.ForType(Sculpture, [MediumSetup], [artwork], ';'), ';');

        Assert.Empty(plan.Errors);
        var addition = Assert.Single(plan.Additions).Addition;
        Assert.Equal(artwork.Name, addition.Name);
        Assert.Equal(artwork.Description, addition.Description);
        Assert.Equal(artwork.DateCreated, addition.DateCreated);
        Assert.Equal(artwork.Dimensions, addition.Dimensions);
        Assert.Equal(artwork.Duration, addition.Duration);
        Assert.Equal([SunriseSunset.Id, Gardens.Id], addition.SeriesIds);
        Assert.Equal([Oil.Id, Bronze.Id], addition.VocabularyTermIds);
        Assert.Empty(addition.Products);
    }

    // the import refuses a column for a field the type doesn't have
    [Fact]
    public void OnlyTheTypesFieldsAreColumns()
    {
        var screenshot = new ArtworkTypeWithFields(new ArtworkTypeId(2), new ArtworkTypeName("Screenshot"), [ArtworkField.DateCreated]);

        var csv = ArtworkCsvExport.ForType(screenshot, [MediumSetup], [], ';');

        Assert.Equal("title,slug,description,dateCreated,series,Medium\r\n", csv);
    }

    [Fact]
    public void AVocabularyNamedLikeAColumnIsPrefixed()
    {
        var screenshot = new ArtworkTypeWithFields(new ArtworkTypeId(2), new ArtworkTypeName("Screenshot"), []);
        var seriesVocabulary = new VocabularySetup(new VocabularyId(4), new VocabularyName("Series"), IsMutuallyExclusive: false, [screenshot.Id], []);

        var csv = ArtworkCsvExport.ForType(screenshot, [seriesVocabulary], [], ';');

        Assert.Equal("title,slug,description,series,vocabulary:Series\r\n", csv);
    }

    [Fact]
    public void TheListSeparatorIsOneNoNameContains()
    {
        var separator = ArtworkCsvExport.ChooseListSeparator(["Night; Day", "Ink | wash", "Plain"]);

        Assert.Equal('/', separator);
    }

    [Fact]
    public void EachProductIsARow()
    {
        var product = new Product(new ProductId(1), Print, "Large", 120.5m, EditionSize: null, Stock: 3);

        var csv = ArtworkCsvExport.Products([MakeArtwork("Dawn", products: [product])]);

        Assert.Equal(
            "artworkType,title,slug,productType,label,price,editionSize,stock\r\nSculpture,Dawn,dawn,Print,Large,120.50,,3\r\n",
            csv
        );
    }
}
