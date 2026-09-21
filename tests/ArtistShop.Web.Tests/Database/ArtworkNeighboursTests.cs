using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkNeighboursTests(TestDatabaseFixture database)
{
    private readonly ArtworkRepository _artworks = new(database.ConnectionFactory);
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);

    private Task<ArtworkIdentifiers> AddPhotographedAsync(SeriesId seriesId, string name) =>
        _catalog.AddPaintingAsync(
            name,
            termIds: [],
            seriesIds: [seriesId],
            images: [CatalogTestData.CreateTestImage()]
        );

    private Task<ArtworkIdentifiers> AddUnphotographedAsync(SeriesId seriesId, string name) =>
        _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [seriesId], images: []);

    // the place the artist dragged each work into, not the order they were added in
    [Fact]
    public async Task FollowsTheOrderTheArtistDragged()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var firstName = $"First {Guid.NewGuid():n}";
        var first = await AddPhotographedAsync(seriesId, firstName);
        var middle = await AddPhotographedAsync(seriesId, $"Middle {Guid.NewGuid():n}");
        var lastName = $"Last {Guid.NewGuid():n}";
        var last = await AddPhotographedAsync(seriesId, lastName);

        await _series.ReorderArtworksAsync(seriesId, [last.Id, middle.Id, first.Id]);

        var neighbours = await _artworks.GetNeighboursInSeriesAsync(
            seriesId,
            middle.Id,
            onlyArtworksWithImages: true
        );

        Assert.Equal(last.Slug, neighbours.Previous?.Slug);
        Assert.Equal(lastName, neighbours.Previous?.Name.Value);
        Assert.Equal(first.Slug, neighbours.Next?.Slug);
        Assert.Equal(firstName, neighbours.Next?.Name.Value);
    }

    [Fact]
    public async Task HasNothingBeyondEitherEnd()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddPhotographedAsync(seriesId, $"First {Guid.NewGuid():n}");
        var last = await AddPhotographedAsync(seriesId, $"Last {Guid.NewGuid():n}");

        var atStart = await _artworks.GetNeighboursInSeriesAsync(
            seriesId,
            first.Id,
            onlyArtworksWithImages: true
        );
        var atEnd = await _artworks.GetNeighboursInSeriesAsync(
            seriesId,
            last.Id,
            onlyArtworksWithImages: true
        );

        Assert.Null(atStart.Previous);
        Assert.Equal(last.Slug, atStart.Next?.Slug);
        Assert.Null(atEnd.Next);
        Assert.Equal(first.Slug, atEnd.Previous?.Slug);
    }

    // a visitor is only sent on to work there is something to look at
    [Fact]
    public async Task StepsOverWorkWithNoPhotograph()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var first = await AddPhotographedAsync(seriesId, $"First {Guid.NewGuid():n}");
        await AddUnphotographedAsync(seriesId, $"Unphotographed {Guid.NewGuid():n}");
        var last = await AddPhotographedAsync(seriesId, $"Last {Guid.NewGuid():n}");

        var neighbours = await _artworks.GetNeighboursInSeriesAsync(
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
        var first = await AddPhotographedAsync(seriesId, $"First {Guid.NewGuid():n}");
        var unphotographed = await AddUnphotographedAsync(
            seriesId,
            $"Unphotographed {Guid.NewGuid():n}"
        );

        var neighbours = await _artworks.GetNeighboursInSeriesAsync(
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
        await AddPhotographedAsync(seriesId, $"In the series {Guid.NewGuid():n}");
        var elsewhere = await _catalog.AddPaintingAsync(
            $"Elsewhere {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [],
            images: [CatalogTestData.CreateTestImage()]
        );

        var neighbours = await _artworks.GetNeighboursInSeriesAsync(
            seriesId,
            elsewhere.Id,
            onlyArtworksWithImages: true
        );

        Assert.Null(neighbours.Previous);
        Assert.Null(neighbours.Next);
    }
}
