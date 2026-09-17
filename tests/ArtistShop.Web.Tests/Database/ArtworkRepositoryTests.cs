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
    private readonly ProductTypeRepository _productTypes = new(database.ConnectionFactory);

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
    public async Task RejectsADeletedProductType()
    {
        var missingType = new ProductTypeId(int.MaxValue);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale product type {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [],
                images: [],
                products: [new ProductAddition(missingType, Label: null, 10m, EditionSize: null, 5)],
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
        var productTypes = await _productTypes.GetAllAsync();
        var original = productTypes.Single(productType => productType.Name.Value == "Original");
        var print = productTypes.Single(productType => productType.Name.Value == "Print");

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
                Assert.Equal(original, sold.Type);
                Assert.Null(sold.Price);
                Assert.Equal(1, sold.EditionSize);
                Assert.Equal(0, sold.Stock);
            },
            openPrint =>
            {
                Assert.Equal(print, openPrint.Type);
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

    private async Task<ArtworkCatalogAddition> PaintingAdditionAsync(string name, IReadOnlyList<SeriesId> seriesIds) =>
        CatalogTestData.CreateArtworkAddition(
            await _catalog.GetPaintingTypeIdAsync(),
            name,
            termIds: [],
            seriesIds,
            images: [],
            products: [],
            duration: null
        );

    [Fact]
    public async Task AddManyAddsEveryArtwork()
    {
        var first = await PaintingAdditionAsync($"Many first {Guid.NewGuid():n}", seriesIds: []);
        var second = await PaintingAdditionAsync($"Many second {Guid.NewGuid():n}", seriesIds: []);

        var identifiers = await _artworks.AddManyAsync([first, second]);

        Assert.Equal(
            [first.Name, second.Name],
            await Task.WhenAll(identifiers.Select(async added => (await _artworks.GetByIdAsync(added.Id))?.Name))
        );
    }

    [Fact]
    public async Task AddManyRollsBackEarlierArtworksWhenALaterOneFails()
    {
        var deletedSeriesId = await _catalog.AddSeriesAsync();
        await _series.DeleteAsync(deletedSeriesId);
        var first = await PaintingAdditionAsync($"Rolled back {Guid.NewGuid():n}", seriesIds: []);
        var stale = await PaintingAdditionAsync($"Stale {Guid.NewGuid():n}", seriesIds: [deletedSeriesId]);

        await Assert.ThrowsAsync<CatalogChangedException>(() => _artworks.AddManyAsync([first, stale]));

        Assert.Null(await _artworks.GetBySlugAsync(first.CandidateSlug.Value));
    }
}
