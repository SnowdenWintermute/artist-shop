using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class WorkImportCatalogSnapshotTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly WorkTypeRepository _workTypes = new(database.Site);
    private readonly VocabularyRepository _vocabularies = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly ProductTypeRepository _productTypes = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);

    private Task<WorkImportCatalogSnapshot?> LoadAsync(WorkTypeId workTypeId) =>
        WorkImportCatalogSnapshot.LoadAsync(workTypeId, _workTypes, _vocabularies, _collections, _productTypes, _works);

    [Fact]
    public async Task LoadsTheTypeAndTheNamesAlreadyInTheCatalog()
    {
        var name = $"Snapshot test {Guid.NewGuid():n}";
        await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);

        var snapshot = await LoadAsync(await _catalog.GetPaintingTypeIdAsync());

        Assert.NotNull(snapshot);
        Assert.Equal("Painting", snapshot.WorkType.Name.Value);
        Assert.Contains(snapshot.TypeWorks, work => work.Title == name && work.Slug == WorkSlug.FromName(name).Value);
        Assert.Contains(snapshot.ProductTypes, productType => productType.Name.Value == "Original");
    }

    // a screenshot and a photograph can be called the same thing; only a title this type
    // already has is a repeat
    [Fact]
    public async Task LeavesOutTheTitlesOfOtherTypes()
    {
        var name = $"Snapshot test {Guid.NewGuid():n}";
        await _catalog.AddWorkAsync(await _catalog.GetTypeIdAsync("Photograph"), name);

        var snapshot = await LoadAsync(await _catalog.GetPaintingTypeIdAsync());

        Assert.NotNull(snapshot);
        Assert.DoesNotContain(snapshot.TypeWorks, work => work.Title == name);
    }

    [Fact]
    public async Task IsNullForAnUnknownType()
    {
        Assert.Null(await LoadAsync(new WorkTypeId(0)));
    }
}
