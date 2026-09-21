using ArtistShop.Web.Database.Repositories;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ProductTypeRepositoryTests(TestDatabaseFixture database)
{
    private readonly ProductTypeRepository _productTypes = new(database.DataSource);

    // the forms that offer a product type read the default off the row, so the seed has to set one.
    // UniqueIndex_ProductTypes_Default is what stops a second appearing
    [Fact]
    public async Task SeedsOriginalAsTheDefault()
    {
        var productTypes = await _productTypes.GetAllAsync();

        var defaultType = Assert.Single(productTypes, productType => productType.IsDefault);
        Assert.Equal("Original", defaultType.Name.Value);
    }
}
