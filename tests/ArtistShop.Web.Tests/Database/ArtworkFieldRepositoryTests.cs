using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkFieldRepositoryTests(TestDatabaseFixture database)
{
    private readonly ArtworkFieldRepository _artworkFields = new(database.ConnectionFactory);

    [Fact]
    public async Task ListsTheSeededFieldsWithDepthUnderHeightAndWidth()
    {
        var definitions = await _artworkFields.GetAllAsync();

        Assert.Equal(
            [
                ArtworkField.DateCreated,
                ArtworkField.HeightAndWidth,
                ArtworkField.Depth,
                ArtworkField.Duration,
            ],
            definitions.Select(definition => definition.Field)
        );
        Assert.Equal(
            [ArtworkField.Depth],
            definitions
                .Where(definition => definition.HasRequirement)
                .Select(definition => definition.Field)
        );
        Assert.Equal(
            ArtworkField.HeightAndWidth,
            definitions.Single(definition => definition.Field == ArtworkField.Depth).RequiredField
        );
    }
}
