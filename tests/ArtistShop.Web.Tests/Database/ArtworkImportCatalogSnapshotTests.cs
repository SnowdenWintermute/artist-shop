using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Database;

public sealed class ArtworkImportCatalogSnapshotTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);
    private readonly ArtworkTypeRepository _artworkTypes = new(database.ConnectionFactory);
    private readonly VocabularyRepository _vocabularies = new(database.ConnectionFactory);
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly ProductTypeRepository _productTypes = new(database.ConnectionFactory);
    private readonly ArtworkRepository _artworks = new(database.ConnectionFactory);

    private Task<ArtworkImportCatalogSnapshot?> LoadAsync(ArtworkTypeId artworkTypeId) =>
        ArtworkImportCatalogSnapshot.LoadAsync(artworkTypeId, _artworkTypes, _vocabularies, _series, _productTypes, _artworks);

    [Fact]
    public async Task LoadsTheTypeAndTheNamesAlreadyInTheCatalog()
    {
        var name = $"Snapshot test {Guid.NewGuid():n}";
        await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);

        var snapshot = await LoadAsync(await _catalog.GetPaintingTypeIdAsync());

        Assert.NotNull(snapshot);
        Assert.Equal("Painting", snapshot.ArtworkType.Name.Value);
        Assert.Contains(name, snapshot.ArtworkNames);
        Assert.Contains(snapshot.ProductTypes, productType => productType.Name.Value == "Original");
    }

    [Fact]
    public async Task IsNullForAnUnknownType()
    {
        Assert.Null(await LoadAsync(new ArtworkTypeId(0)));
    }
}
