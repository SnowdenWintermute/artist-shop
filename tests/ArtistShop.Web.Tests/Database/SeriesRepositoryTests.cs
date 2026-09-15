using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Microsoft.Data.SqlClient;

namespace ArtistShop.Web.Tests.Database;

public sealed class SeriesRepositoryTests(TestDatabaseFixture database)
{
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly PaintingRepository _paintings = new(database.ConnectionFactory);
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);

    private Task<SeriesId> AddNamedSeriesAsync(string name) =>
        _series.AddAsync(new SeriesName(name), SeriesSlug.FromName(name));

    private async Task<SeriesWithShopItems> GetExistingAsync(SeriesId id) =>
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
    public async Task ListsShopItemsInOrderWithTheirPrimaryImages()
    {
        var id = await _catalog.AddSeriesAsync();
        var image = CatalogTestData.CreateTestImage();
        var withImageId = await _catalog.AddPaintingInSeriesAsync(id, [image]);
        var withoutImageId = await _catalog.AddPaintingInSeriesAsync(id, []);

        var series = await GetExistingAsync(id);

        Assert.Equal(
            [withImageId, withoutImageId],
            series.ShopItems.Select(shopItem => shopItem.Id)
        );
        Assert.Equal(image, series.ShopItems[0].PrimaryImage);
        Assert.Null(series.ShopItems[1].PrimaryImage);
        Assert.Equal(new ShopItemTypeName("Painting"), series.ShopItems[0].ShopItemTypeName);
    }

    [Fact]
    public async Task CoverFallsBackToTheFirstShopItemWithAnImage()
    {
        var id = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingInSeriesAsync(id, []);
        var image = CatalogTestData.CreateTestImage();
        await _catalog.AddPaintingInSeriesAsync(id, [image]);
        await _catalog.AddPaintingInSeriesAsync(id, [CatalogTestData.CreateTestImage()]);

        var series = (await _series.GetAllWithCoversAsync()).Single(series => series.Id == id);

        Assert.Equal(3, series.ShopItemCount);
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
        Assert.Equal(0, allSeries.Single(series => series.Id == emptyId).ShopItemCount);
        Assert.Null(allSeries.Single(series => series.Id == imagelessId).Cover);
    }

    [Fact]
    public async Task StarredShopItemIsTheCover()
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
            (await GetExistingAsync(id)).ShopItems.Select(shopItem => shopItem.IsCover)
        );
    }

    [Fact]
    public async Task RejectsACoverWithNoImage()
    {
        var id = await _catalog.AddSeriesAsync();
        var shopItemId = await _catalog.AddPaintingInSeriesAsync(id, []);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            _series.SetCoverAsync(id, shopItemId)
        );
        Assert.Equal(50007, exception.Number);
    }

    [Fact]
    public async Task ReorderSwapsShopItems()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var thirdId = await _catalog.AddPaintingInSeriesAsync(id, []);

        await _series.ReorderShopItemsAsync(id, [thirdId, secondId, firstId]);

        Assert.Equal(
            [thirdId, secondId, firstId],
            (await GetExistingAsync(id)).ShopItems.Select(shopItem => shopItem.Id)
        );
    }

    [Fact]
    public async Task ReorderRejectsAListThatDoesNotMatchTheSeries()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        await _catalog.AddPaintingInSeriesAsync(id, []);

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            _series.ReorderShopItemsAsync(id, [firstId])
        );
        Assert.Equal(50005, exception.Number);
    }

    [Fact]
    public async Task RemovesOnlyTheChosenShopItems()
    {
        var id = await _catalog.AddSeriesAsync();
        var firstId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var secondId = await _catalog.AddPaintingInSeriesAsync(id, []);
        var thirdId = await _catalog.AddPaintingInSeriesAsync(id, []);

        await _series.RemoveShopItemsAsync(id, [secondId]);

        Assert.Equal(
            [firstId, thirdId],
            (await GetExistingAsync(id)).ShopItems.Select(shopItem => shopItem.Id)
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
    public async Task DeleteKeepsTheSeriesPaintings()
    {
        var id = await _catalog.AddSeriesAsync();
        var painting = await _catalog.AddPaintingAsync(
            $"Kept painting {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [id],
            images: []
        );

        await _series.DeleteAsync(id);

        var keptPainting = await _paintings.GetBySlugAsync(painting.Slug.Value);
        Assert.NotNull(keptPainting);
        Assert.Empty(keptPainting.Series);
    }
}
