using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class SeriesRepositoryTests(TestDatabaseFixture database)
{
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly ArtworkRepository _artworks = new(database.ConnectionFactory);
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);

    private Task<SeriesId> AddNamedSeriesAsync(string name) =>
        _series.AddAsync(new SeriesName(name), SeriesSlug.FromName(name));

    private async Task<SeriesWithArtworks> GetExistingAsync(SeriesId id) =>
        await _series.GetAsync(id) ?? throw new InvalidOperationException("The series is missing.");

    // different names can share a slug when they differ only in what the slug drops:
    // punctuation, accents, or letter case
    [Fact]
    public async Task RejectsANameWhoseSlugAnotherSeriesHas()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedSeriesAsync(name);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => AddNamedSeriesAsync($"{name}!"));
    }

    [Fact]
    public async Task RenameRejectsANameWhoseSlugAnotherSeriesHas()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedSeriesAsync(name);
        var id = await _catalog.AddSeriesAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _series.RenameAsync(id, new SeriesName($"{name}!"), SeriesSlug.FromName($"{name}!"))
        );
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedSeriesAsync(name);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => AddNamedSeriesAsync(name));
    }

    [Fact]
    public async Task NewSeriesGoesLast()
    {
        var id = await _catalog.AddSeriesAsync();

        Assert.Equal(id, (await _series.GetAllWithCoversAsync()).Last().Id);
    }

    // reordering takes every series, so these rely on nothing else adding one while they run.
    // Within a class xUnit runs one test at a time; across classes that is what DatabaseCollection is for
    [Fact]
    public async Task ReorderSetsTheOrderOfAllSeries()
    {
        await _catalog.AddSeriesAsync();
        await _catalog.AddSeriesAsync();
        List<SeriesId> reversed = [.. (await _series.GetAllWithCoversAsync()).Select(series => series.Id).Reverse()];

        await _series.ReorderAsync(reversed);

        Assert.Equal(reversed, (await _series.GetAllWithCoversAsync()).Select(series => series.Id));
    }

    [Fact]
    public async Task GetAllListsSeriesInTheArtistsOrder()
    {
        await _catalog.AddSeriesAsync();
        await _catalog.AddSeriesAsync();
        List<SeriesId> reversed = [.. (await _series.GetAllWithCoversAsync()).Select(series => series.Id).Reverse()];
        await _series.ReorderAsync(reversed);

        Assert.Equal(reversed, (await _series.GetAllAsync()).Select(series => series.Id));
    }

    [Fact]
    public async Task ArtworkAddedToASeriesJoinsItLast()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, []);

        Assert.Equal([firstId, secondId], (await GetExistingAsync(id)).Artworks.Select(artwork => artwork.Id));
    }

    [Fact]
    public async Task ReorderRejectsAListMissingASeries()
    {
        await _catalog.AddSeriesAsync();
        List<SeriesId> allButOne = [.. (await _series.GetAllWithCoversAsync()).Select(series => series.Id).Skip(1)];

        await Assert.ThrowsAsync<CatalogChangedException>(() => _series.ReorderAsync(allButOne));
    }

    [Fact]
    public async Task RenameMovesTheSlugWithTheName()
    {
        var id = await _catalog.AddSeriesAsync();
        var newName = $"Green {Guid.NewGuid():n}";

        await _series.RenameAsync(id, new SeriesName(newName), SeriesSlug.FromName(newName));

        var series = await GetExistingAsync(id);
        Assert.Equal(new SeriesName(newName), series.Name);
        Assert.Equal(SeriesSlug.FromName(newName), series.Slug);
    }

    [Fact]
    public async Task RenameCanKeepItsOwnSlug()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        var id = await AddNamedSeriesAsync(name);

        await _series.RenameAsync(id, new SeriesName($"{name}!"), SeriesSlug.FromName($"{name}!"));

        Assert.Equal(SeriesSlug.FromName(name), (await GetExistingAsync(id)).Slug);
    }

    [Fact]
    public async Task ListsArtworksInOrderWithTheirPrimaryImages()
    {
        var id = await _catalog.AddSeriesAsync();
        var image = CatalogTestData.CreateTestImage();
        var withImageId = await _catalog.AddPaintingInSeriesAsync(id, [image]);
        var withoutImageId = await _catalog.AddPaintingInSeriesAsync(id, []);

        var series = await GetExistingAsync(id);

        Assert.Equal(
            [withImageId, withoutImageId],
            series.Artworks.Select(artwork => artwork.Id)
        );
        Assert.Equal(image, series.Artworks[0].PrimaryImage);
        Assert.Null(series.Artworks[1].PrimaryImage);
        Assert.Equal(new ArtworkTypeName("Painting"), series.Artworks[0].ArtworkTypeName);
    }

    [Fact]
    public async Task CoverFallsBackToTheFirstArtworkWithAnImage()
    {
        var id = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingInSeriesAsync(id, []);
        var image = CatalogTestData.CreateTestImage();
        await _catalog.AddPaintingInSeriesAsync(id, [image]);
        await _catalog.AddPaintingInSeriesAsync(id, [CatalogTestData.CreateTestImage()]);

        var series = (await _series.GetAllWithCoversAsync()).Single(series => series.Id == id);

        Assert.Equal(3, series.ArtworkCount);
        Assert.Equal(image, series.Cover);
    }

    [Fact]
    public async Task SeriesWithoutImagesHasNoCover()
    {
        var emptyId = await _catalog.AddSeriesAsync();
        var imagelessId = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingInSeriesAsync(imagelessId, []);

        var allSeries = await _series.GetAllWithCoversAsync();

        Assert.Null(allSeries.Single(series => series.Id == emptyId).Cover);
        Assert.Equal(0, allSeries.Single(series => series.Id == emptyId).ArtworkCount);
        Assert.Null(allSeries.Single(series => series.Id == imagelessId).Cover);
    }

    [Fact]
    public async Task StarredArtworkIsTheCover()
    {
        var id = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingInSeriesAsync(id, [CatalogTestData.CreateTestImage()]);
        var secondImage = CatalogTestData.CreateTestImage();
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, [secondImage]);

        await _series.SetCoverAsync(id, secondId);

        var series = (await _series.GetAllWithCoversAsync()).Single(series => series.Id == id);
        Assert.Equal(secondImage, series.Cover);
    }

    [Fact]
    public async Task MovingTheStarReplacesTheCover()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, [CatalogTestData.CreateTestImage()]);
        var secondImage = CatalogTestData.CreateTestImage();
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, [secondImage]);
        await _series.SetCoverAsync(id, firstId);

        await _series.SetCoverAsync(id, secondId);

        var series = (await _series.GetAllWithCoversAsync()).Single(series => series.Id == id);
        Assert.Equal(secondImage, series.Cover);
        Assert.Equal(
            [false, true],
            (await GetExistingAsync(id)).Artworks.Select(artwork => artwork.IsCover)
        );
    }

    [Fact]
    public async Task ClearingTheStarFallsBackToTheFirstArtworkWithAnImage()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstImage = CatalogTestData.CreateTestImage();
        await _catalog.AddPaintingInSeriesAsync(id, [firstImage]);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, [CatalogTestData.CreateTestImage()]);
        await _series.SetCoverAsync(id, secondId);

        await _series.ClearCoverAsync(id);

        var series = (await _series.GetAllWithCoversAsync()).Single(series => series.Id == id);
        Assert.Equal(firstImage, series.Cover);
        Assert.All((await GetExistingAsync(id)).Artworks, artwork => Assert.False(artwork.IsCover));
    }

    [Fact]
    public async Task RejectsACoverWithNoImage()
    {
        var id = await _catalog.AddSeriesAsync();
        var artworkId = await _catalog.AddPaintingInSeriesAsync(id, []);

        await Assert.ThrowsAsync<CatalogChangedException>(() => _series.SetCoverAsync(id, artworkId));
    }

    [Fact]
    public async Task ReorderSwapsArtworks()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var thirdId = await _catalog.AddPaintingInSeriesAsync(id, []);

        await _series.ReorderArtworksAsync(id, [thirdId, secondId, firstId]);

        Assert.Equal(
            [thirdId, secondId, firstId],
            (await GetExistingAsync(id)).Artworks.Select(artwork => artwork.Id)
        );
    }

    [Fact]
    public async Task ReorderRejectsAListThatDoesNotMatchTheSeries()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        await _catalog.AddPaintingInSeriesAsync(id, []);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _series.ReorderArtworksAsync(id, [firstId])
        );
    }

    [Fact]
    public async Task RenameRejectsADeletedSeries()
    {
        var id = await _catalog.AddSeriesAsync();
        await _series.DeleteAsync(id);
        var name = $"Renamed {Guid.NewGuid():n}";

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _series.RenameAsync(id, new SeriesName(name), SeriesSlug.FromName(name))
        );
    }

    [Fact]
    public async Task RemovesOnlyTheChosenArtworks()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var thirdId = await _catalog.AddPaintingInSeriesAsync(id, []);

        await _series.RemoveArtworksAsync(id, [secondId]);

        Assert.Equal(
            [firstId, thirdId],
            (await GetExistingAsync(id)).Artworks.Select(artwork => artwork.Id)
        );
    }

    [Fact]
    public async Task DeleteRemovesTheSeries()
    {
        var id = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingInSeriesAsync(id, []);

        await _series.DeleteAsync(id);

        Assert.Null(await _series.GetAsync(id));
    }

    [Fact]
    public async Task DeleteKeepsTheSeriesArtworks()
    {
        var id = await _catalog.AddSeriesAsync();
        var painting = await _catalog.AddPaintingAsync(
            $"Kept painting {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [id],
            images: []
        );

        await _series.DeleteAsync(id);

        var keptPainting = await _artworks.GetBySlugAsync(painting.Slug.Value);
        Assert.NotNull(keptPainting);
        Assert.Empty(keptPainting.Series);
    }
}
