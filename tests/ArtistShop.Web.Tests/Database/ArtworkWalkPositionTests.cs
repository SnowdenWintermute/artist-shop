using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

// Other tests' series share the database, so these compare positions within the series each test
// adds rather than reading them whole. New series go last, which the one test of the total leans on
[Collection(DatabaseCollection.Name)]
public sealed class ArtworkWalkPositionTests(TestDatabaseFixture database)
{
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly SeriesRepository _series = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<ArtworkIdentifiers> AddAsync(SeriesId seriesId, int imageCount) =>
        _catalog.AddPaintingInSeriesAsync(
            seriesId,
            [.. Enumerable.Range(0, imageCount).Select(_ => CatalogTestData.CreateTestImage())]
        );

    private async Task<int> EarlierImageCountAsync(SeriesId seriesId, ArtworkIdentifiers artwork) =>
        (await _artworks.GetWalkPositionAsync(seriesId, artwork.Id)).EarlierImageCount;

    // each artwork's pictures follow on from the one before's, in the place the artist dragged
    // them into rather than the order they were added in
    [Fact]
    public async Task CountsTheImagesBeforeInTheOrderTheArtistDragged()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddAsync(seriesId, imageCount: 1);
        var middle = await AddAsync(seriesId, imageCount: 2);
        var last = await AddAsync(seriesId, imageCount: 3);

        await _series.ReorderArtworksAsync(seriesId, [last.Id, middle.Id, first.Id]);

        var lastsPlace = await EarlierImageCountAsync(seriesId, last);

        Assert.Equal(lastsPlace + 3, await EarlierImageCountAsync(seriesId, middle));
        Assert.Equal(lastsPlace + 3 + 2, await EarlierImageCountAsync(seriesId, first));
    }

    // the walk runs on from one series into the next, and work with no photograph adds nothing,
    // as Previous and Next step over it
    [Fact]
    public async Task RunsOnAcrossSeriesPastUnphotographedWork()
    {
        var firstSeries = await _catalog.AddSeriesAsync();
        var inFirst = await AddAsync(firstSeries, imageCount: 2);

        var between = await _catalog.AddSeriesAsync();
        await AddAsync(between, imageCount: 0);

        var lastSeries = await _catalog.AddSeriesAsync();
        var inLast = await AddAsync(lastSeries, imageCount: 1);

        Assert.Equal(
            await EarlierImageCountAsync(firstSeries, inFirst) + 2,
            await EarlierImageCountAsync(lastSeries, inLast)
        );
    }

    // counted once for each place it has in the walk, since the walk visits it once in each
    [Fact]
    public async Task CountsAnArtworkInTwoSeriesInBoth()
    {
        var firstSeries = await _catalog.AddSeriesAsync();
        var lastSeries = await _catalog.AddSeriesAsync();
        var inBoth = await _catalog.AddPaintingAsync(
            $"Series test painting {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [firstSeries, lastSeries],
            images: [CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage()]
        );
        var after = await AddAsync(lastSeries, imageCount: 1);

        var firstPlace = await EarlierImageCountAsync(firstSeries, inBoth);

        Assert.Equal(firstPlace + 2, await EarlierImageCountAsync(lastSeries, inBoth));
        Assert.Equal(firstPlace + 2 + 2, await EarlierImageCountAsync(lastSeries, after));
    }

    // the last artwork of the last series has nothing after it but its own pictures
    [Fact]
    public async Task TotalEndsWithTheLastArtworksPictures()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var last = await AddAsync(seriesId, imageCount: 3);

        var position = await _artworks.GetWalkPositionAsync(seriesId, last.Id);

        Assert.Equal(position.EarlierImageCount + 3, position.TotalImageCount);
    }
}
