using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

// Other tests' series share the database, and new series go last, so these only look between the
// series each test adds, and past the last of them
[Collection(DatabaseCollection.Name)]
public sealed class ArtworksBeyondSeriesTests(TestDatabaseFixture database)
{
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<ArtworkIdentifiers> AddAsync(SeriesId seriesId, int imageCount) =>
        _catalog.AddPaintingAsync(
            $"Painting {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: [.. Enumerable.Range(0, imageCount).Select(_ => CatalogTestData.CreateTestImage())]
        );

    private async Task<SeriesSlug> SlugOfAsync(SeriesId seriesId) =>
        (await new SeriesRepository(database.Site).GetAsync(seriesId))?.Slug
        ?? throw new InvalidOperationException($"Series {seriesId.Value} wasn't added.");

    // the series between has only unphotographed work, so a visitor steps over it
    [Fact]
    public async Task StepsToTheNearestSeriesWithSomethingToSee()
    {
        var first = await _catalog.AddSeriesAsync();
        await AddAsync(first, imageCount: 1);
        var firstsLast = await AddAsync(first, imageCount: 3);

        var between = await _catalog.AddSeriesAsync();
        await AddAsync(between, imageCount: 0);

        var last = await _catalog.AddSeriesAsync();
        var lastsFirst = await AddAsync(last, imageCount: 2);
        await AddAsync(last, imageCount: 1);

        var afterFirst = await _artworks.GetBeyondSeriesAsync(first, onlyArtworksWithImages: true);
        var beforeLast = await _artworks.GetBeyondSeriesAsync(last, onlyArtworksWithImages: true);

        Assert.Equal(new ArtworkInSeries(lastsFirst.Slug, await SlugOfAsync(last), 2), afterFirst.Next);
        Assert.Equal(new ArtworkInSeries(firstsLast.Slug, await SlugOfAsync(first), 3), beforeLast.Previous);
        Assert.Null(beforeLast.Next);
    }

    // the artist sees everything, so the rule is a parameter rather than the procedure's own
    [Fact]
    public async Task KeepsUnphotographedWorkForTheArtist()
    {
        var first = await _catalog.AddSeriesAsync();
        await AddAsync(first, imageCount: 1);

        var last = await _catalog.AddSeriesAsync();
        var unphotographed = await AddAsync(last, imageCount: 0);

        var afterFirst = await _artworks.GetBeyondSeriesAsync(first, onlyArtworksWithImages: false);

        Assert.Equal(new ArtworkInSeries(unphotographed.Slug, await SlugOfAsync(last), 0), afterFirst.Next);
    }
}
