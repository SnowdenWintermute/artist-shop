using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

// Other tests' series share the database, and new series go last, so these only look within or
// between the series each test adds, and past the last of them
[Collection(DatabaseCollection.Name)]
public sealed class ArtworkNeighboursTests(TestDatabaseFixture database)
{
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly SeriesRepository _series = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<ArtworkIdentifiers> AddAsync(SeriesId seriesId, int imageCount) =>
        _catalog.AddPaintingInSeriesAsync(
            seriesId,
            [.. Enumerable.Range(0, imageCount).Select(_ => CatalogTestData.CreateTestImage())]
        );

    private async Task<SeriesSlug> SlugOfAsync(SeriesId seriesId) =>
        (await _series.GetAsync(seriesId))?.Slug
        ?? throw new InvalidOperationException($"Series {seriesId.Value} wasn't added.");

    // the place the artist dragged each work into, not the order they were added in
    [Fact]
    public async Task FollowsTheOrderTheArtistDragged()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddAsync(seriesId, imageCount: 1);
        var middle = await AddAsync(seriesId, imageCount: 1);
        var last = await AddAsync(seriesId, imageCount: 1);

        await _series.ReorderArtworksAsync(seriesId, [last.Id, middle.Id, first.Id]);

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            middle.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(last.Slug, neighbours.Previous?.Slug);
        Assert.Equal(first.Slug, neighbours.Next?.Slug);
    }

    // so stepping back past the first image can land on the previous artwork's last
    [Fact]
    public async Task CountsEachNeighboursImages()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        await AddAsync(seriesId, imageCount: 3);
        var middle = await AddAsync(seriesId, imageCount: 1);
        await AddAsync(seriesId, imageCount: 1);

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            middle.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(3, neighbours.Previous?.ImageCount);
        Assert.Equal(1, neighbours.Next?.ImageCount);
    }

    // past either end of a series comes the series beside it, and the series between has only
    // unphotographed work, so a visitor steps over it
    [Fact]
    public async Task RunsOnIntoTheNearestSeriesWithSomethingToSee()
    {
        var first = await _catalog.AddSeriesAsync();
        await AddAsync(first, imageCount: 1);
        var firstsLast = await AddAsync(first, imageCount: 3);

        var between = await _catalog.AddSeriesAsync();
        await AddAsync(between, imageCount: 0);

        var last = await _catalog.AddSeriesAsync();
        var lastsFirst = await AddAsync(last, imageCount: 2);
        await AddAsync(last, imageCount: 1);

        var fromFirstsLast = await _artworks.GetNeighboursAsync(
            first,
            firstsLast.Id,
            onlyArtworksWithImages: true
        );
        var fromLastsFirst = await _artworks.GetNeighboursAsync(
            last,
            lastsFirst.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(new ArtworkInSeries(lastsFirst.Slug, await SlugOfAsync(last), 2), fromFirstsLast.Next);
        Assert.Equal(new ArtworkInSeries(firstsLast.Slug, await SlugOfAsync(first), 3), fromLastsFirst.Previous);
    }

    [Fact]
    public async Task HasNothingPastTheLastSeries()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddAsync(seriesId, imageCount: 1);
        var last = await AddAsync(seriesId, imageCount: 1);

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            last.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(first.Slug, neighbours.Previous?.Slug);
        Assert.Null(neighbours.Next);
    }

    // a visitor is only sent on to work there is something to look at
    [Fact]
    public async Task StepsOverWorkWithNoPhotograph()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddAsync(seriesId, imageCount: 1);
        await AddAsync(seriesId, imageCount: 0);
        var last = await AddAsync(seriesId, imageCount: 1);

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            first.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(last.Slug, neighbours.Next?.Slug);
    }

    // the artist sees everything, so the rule is a parameter rather than the procedure's own
    [Fact]
    public async Task KeepsUnphotographedWorkForTheArtist()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddAsync(seriesId, imageCount: 1);
        var unphotographed = await AddAsync(seriesId, imageCount: 0);

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            first.Id,
            onlyArtworksWithImages: false
        );

        Assert.Equal(unphotographed.Slug, neighbours.Next?.Slug);
    }

    // an artwork that isn't in the series anchors nothing, so neither side is offered
    [Fact]
    public async Task HasNoNeighboursOutsideTheSeries()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        await AddAsync(seriesId, imageCount: 1);
        var elsewhere = await _catalog.AddPaintingAsync(
            $"Elsewhere {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [],
            images: [CatalogTestData.CreateTestImage()]
        );

        var neighbours = await _artworks.GetNeighboursAsync(
            seriesId,
            elsewhere.Id,
            onlyArtworksWithImages: true
        );

        Assert.Null(neighbours.Previous);
        Assert.Null(neighbours.Next);
    }
}
