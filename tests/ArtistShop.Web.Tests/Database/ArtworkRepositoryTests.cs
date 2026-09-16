using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

public sealed class ArtworkRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly VocabularyTermRepository _terms = new(database.ConnectionFactory);
    private readonly ArtworkRepository _artworks = new(database.ConnectionFactory);
    private readonly ProductKindRepository _productKinds = new(database.ConnectionFactory);

    [Fact]
    public async Task NumbersTheSlugWhenItIsTaken()
    {
        var name = $"Sunset {Guid.NewGuid():n}";

        var first = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);
        var second = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);
        var third = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);

        Assert.Equal($"{first.Slug.Value}-2", second.Slug.Value);
        Assert.Equal($"{first.Slug.Value}-3", third.Slug.Value);
    }

    // each stands for a choice that changed while the add-artwork form was open
    [Fact]
    public async Task RejectsADeletedVocabularyTerm()
    {
        var termId = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        await _terms.DeleteAsync(termId);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale term {Guid.NewGuid():n}",
                termIds: [termId],
                seriesIds: [],
                images: []
            )
        );
    }

    [Fact]
    public async Task RejectsADeletedSeries()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        await _series.DeleteAsync(seriesId);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale series {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [seriesId],
                images: []
            )
        );
    }

    [Fact]
    public async Task RejectsADeletedProductKind()
    {
        var missingKind = new ProductKindId(int.MaxValue);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale kind {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [],
                images: [],
                products: [new ProductAddition(missingKind, Label: null, 10m, EditionSize: null, 5)],
                duration: null
            )
        );
    }

    // the seeded painting type has no duration field
    [Fact]
    public async Task RejectsAValueForAFieldTheTypeDoesNotHave()
    {
        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale field {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [],
                images: [],
                products: [],
                duration: TimeSpan.FromMinutes(3)
            )
        );
    }

    [Fact]
    public async Task ReadsBackTheTypeAndProducts()
    {
        var kinds = await _productKinds.GetAllAsync();
        var original = kinds.Single(kind => kind.Name.Value == "Original");
        var print = kinds.Single(kind => kind.Name.Value == "Print");

        var identifiers = await _catalog.AddPaintingAsync(
            $"With products {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [],
            images: [],
            products:
            [
                new ProductAddition(original.Id, Label: null, Price: null, EditionSize: 1, Stock: 0),
                new ProductAddition(print.Id, "A4", 40m, EditionSize: null, Stock: 100),
            ],
            duration: null
        );

        var artwork = await _artworks.GetByIdAsync(identifiers.Id);

        Assert.NotNull(artwork);
        Assert.Equal(await _catalog.GetPaintingTypeIdAsync(), artwork.Type.Id);
        Assert.Collection(
            artwork.Products,
            sold =>
            {
                Assert.Equal(original, sold.Kind);
                Assert.Null(sold.Price);
                Assert.Equal(1, sold.EditionSize);
                Assert.Equal(0, sold.Stock);
            },
            openPrint =>
            {
                Assert.Equal(print, openPrint.Kind);
                Assert.Equal("A4", openPrint.Label);
                Assert.Equal(40m, openPrint.Price);
                Assert.Null(openPrint.EditionSize);
                Assert.Equal(100, openPrint.Stock);
            }
        );
    }

    [Fact]
    public async Task FindsTheSameArtworkBySlugAndById()
    {
        var identifiers = await _catalog.AddPaintingAsync(
            $"Lookup {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [],
            images: []
        );

        var bySlug = await _artworks.GetBySlugAsync(identifiers.Slug.Value);

        Assert.NotNull(bySlug);
        Assert.Equal(identifiers.Id, bySlug.Id);
        Assert.Null(await _artworks.GetBySlugAsync($"missing-{Guid.NewGuid():n}"));
    }

    [Fact]
    public async Task RejectsDepthOnAPhotograph()
    {
        var photographTypeId = await _catalog.GetTypeIdAsync("Photograph");
        var withDepth = new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m));

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddArtworkWithDimensionsAsync(photographTypeId, withDepth)
        );
    }

    [Fact]
    public async Task KeepsHeightWidthAndDepthApart()
    {
        var sculptureTypeId = await _catalog.GetTypeIdAsync("Sculpture");
        var dimensions = new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m));

        var identifiers = await _catalog.AddArtworkWithDimensionsAsync(sculptureTypeId, dimensions);
        var artwork = await _artworks.GetByIdAsync(identifiers.Id);

        Assert.NotNull(artwork);
        Assert.Equal(dimensions, artwork.Dimensions);
    }
}
