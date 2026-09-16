using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

public sealed class ArtworkTypeRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);
    private readonly ArtworkTypeRepository _artworkTypes = new(database.ConnectionFactory);

    // also checks the ArtworkField enum values line up with the seeded ids
    [Fact]
    public async Task ListsTheSeededFieldsPerType()
    {
        var photographFields = await _artworkTypes.GetFieldsAsync(
            await _catalog.GetTypeIdAsync("Photograph")
        );
        var sculptureFields = await _artworkTypes.GetFieldsAsync(
            await _catalog.GetTypeIdAsync("Sculpture")
        );

        Assert.Equal(
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth],
            photographFields.Order()
        );
        Assert.Equal(
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth],
            sculptureFields.Order()
        );
    }
}
