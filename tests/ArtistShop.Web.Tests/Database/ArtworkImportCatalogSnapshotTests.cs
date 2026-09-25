using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkImportCatalogSnapshotTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly ArtworkTypeRepository _artworkTypes = new(database.Site);
    private readonly VocabularyRepository _vocabularies = new(database.Site);
    private readonly SeriesRepository _series = new(database.Site);
    private readonly ProductTypeRepository _productTypes = new(database.Site);
    private readonly ArtworkRepository _artworks = new(database.Site);

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
        Assert.Contains(name, snapshot.TypeArtworkNames);
        Assert.Contains(snapshot.ProductTypes, productType => productType.Name.Value == "Original");
    }

    // a screenshot and a photograph can be called the same thing; only a title this type
    // already has is a repeat
    [Fact]
    public async Task LeavesOutTheTitlesOfOtherTypes()
    {
        var name = $"Snapshot test {Guid.NewGuid():n}";
        await _catalog.AddArtworkAsync(await _catalog.GetTypeIdAsync("Photograph"), name);

        var snapshot = await LoadAsync(await _catalog.GetPaintingTypeIdAsync());

        Assert.NotNull(snapshot);
        Assert.DoesNotContain(name, snapshot.TypeArtworkNames);
    }

    [Fact]
    public async Task IsNullForAnUnknownType()
    {
        Assert.Null(await LoadAsync(new ArtworkTypeId(0)));
    }
}
