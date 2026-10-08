using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class WorkFieldRepositoryTests(TestDatabaseFixture database)
{
    private readonly WorkFieldRepository _workFields = new(database.Site);

    [Fact]
    public async Task ListsTheSeededFieldsWithDepthUnderHeightAndWidth()
    {
        var definitions = await _workFields.GetAllAsync();

        Assert.Equal(
            [
                WorkField.DateCreated,
                WorkField.HeightAndWidth,
                WorkField.Depth,
                WorkField.Duration,
            ],
            definitions.Select(definition => definition.Field)
        );
        Assert.Equal(
            [WorkField.Depth],
            definitions
                .Where(definition => definition.HasRequirement)
                .Select(definition => definition.Field)
        );
        Assert.Equal(
            WorkField.HeightAndWidth,
            definitions.Single(definition => definition.Field == WorkField.Depth).RequiredField
        );
    }
}
