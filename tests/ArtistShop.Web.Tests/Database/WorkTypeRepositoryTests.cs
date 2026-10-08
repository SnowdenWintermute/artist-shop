using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class WorkTypeRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly WorkTypeRepository _workTypes = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly VocabularyRepository _vocabularies = new(database.Site);

    // test classes share the database in parallel, so names must not collide
    private static WorkTypeName UniqueName() => new($"Type {Guid.NewGuid():n}");

    // also checks the WorkField enum values line up with the seeded ids
    [Fact]
    public async Task ListsTheSeededFieldsPerType()
    {
        var photograph = await _workTypes.GetAsync(await _catalog.GetTypeIdAsync("Photograph"));
        var sculpture = await _workTypes.GetAsync(await _catalog.GetTypeIdAsync("Sculpture"));

        Assert.NotNull(photograph);
        Assert.NotNull(sculpture);
        Assert.Equal([WorkField.DateCreated, WorkField.HeightAndWidth], photograph.Fields);
        Assert.Equal(
            [WorkField.DateCreated, WorkField.HeightAndWidth, WorkField.Depth],
            sculpture.Fields
        );
    }

    [Fact]
    public async Task AddedTypeHasItsNameAndFields()
    {
        var name = UniqueName();

        var id = await _workTypes.AddAsync(
            name,
            [WorkField.Depth, WorkField.HeightAndWidth, WorkField.Duration]
        );

        var workType = await _workTypes.GetAsync(id);
        Assert.NotNull(workType);
        Assert.Equal(name, workType.Name);
        Assert.Equal(
            [WorkField.HeightAndWidth, WorkField.Depth, WorkField.Duration],
            workType.Fields
        );
        Assert.Contains(new WorkType(id, name), await _workTypes.GetAllAsync());
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = UniqueName();
        await _workTypes.AddAsync(name, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _workTypes.AddAsync(name, [])
        );
    }

    [Fact]
    public async Task RefusesDepthWithoutHeightAndWidth()
    {
        await Assert.ThrowsAsync<PostgresException>(() =>
            _workTypes.AddAsync(UniqueName(), [WorkField.Depth])
        );
    }

    [Fact]
    public async Task GetReturnsNullForUnknownId()
    {
        // ids start at 1
        Assert.Null(await _workTypes.GetAsync(new WorkTypeId(0)));
    }

    [Fact]
    public async Task UpdateRenamesAndSwitchesFields()
    {
        var id = await _workTypes.AddAsync(
            UniqueName(),
            [WorkField.DateCreated, WorkField.HeightAndWidth, WorkField.Depth]
        );
        var newName = UniqueName();

        await _workTypes.UpdateAsync(id, newName, [WorkField.Duration]);

        var workType = await _workTypes.GetAsync(id);
        Assert.NotNull(workType);
        Assert.Equal(newName, workType.Name);
        Assert.Equal([WorkField.Duration], workType.Fields);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateName()
    {
        var takenName = UniqueName();
        await _workTypes.AddAsync(takenName, []);
        var id = await _workTypes.AddAsync(UniqueName(), []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _workTypes.UpdateAsync(id, takenName, [])
        );
    }

    [Fact]
    public async Task UpdateOfDeletedTypeThrowsChangedSincePageLoad()
    {
        var id = await _workTypes.AddAsync(UniqueName(), []);
        await _workTypes.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _workTypes.UpdateAsync(id, UniqueName(), [])
        );
    }

    [Fact]
    public async Task SwitchingFieldsOffClearsTheirValues()
    {
        var id = await _workTypes.AddAsync(
            UniqueName(),
            [WorkField.HeightAndWidth, WorkField.Depth]
        );
        var identifiers = await _catalog.AddWorkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m))
        );

        var counts = await _workTypes.CountWorksAsync(id);
        Assert.Equal(new WorkTypeWorkCounts(1, 0, 1, 1, 0), counts);

        await _workTypes.UpdateAsync(id, UniqueName(), [WorkField.DateCreated]);

        var work = await _works.GetByIdAsync(identifiers.Id);
        Assert.NotNull(work);
        Assert.Null(work.Dimensions);
    }

    [Fact]
    public async Task SwitchingDepthOffKeepsHeightAndWidth()
    {
        var id = await _workTypes.AddAsync(
            UniqueName(),
            [WorkField.HeightAndWidth, WorkField.Depth]
        );
        var identifiers = await _catalog.AddWorkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m))
        );

        await _workTypes.UpdateAsync(id, UniqueName(), [WorkField.HeightAndWidth]);

        var work = await _works.GetByIdAsync(identifiers.Id);
        Assert.NotNull(work);
        Assert.Equal(
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null)),
            work.Dimensions
        );
    }

    [Fact]
    public async Task DeleteRemovesUnusedTypeAndItsVocabularyLinks()
    {
        var id = await _workTypes.AddAsync(UniqueName(), [WorkField.DateCreated]);
        var vocabularyId = await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            isMutuallyExclusive: false,
            [id]
        );

        await _workTypes.DeleteAsync(id);

        Assert.Null(await _workTypes.GetAsync(id));
        var vocabulary = await _vocabularies.GetAsync(vocabularyId);
        Assert.NotNull(vocabulary);
        Assert.Empty(vocabulary.WorkTypeIds);
    }

    [Fact]
    public async Task DeleteRefusesTypeInUse()
    {
        var id = await _workTypes.AddAsync(UniqueName(), [WorkField.HeightAndWidth]);
        await _catalog.AddWorkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null))
        );

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _workTypes.DeleteAsync(id));
        Assert.NotNull(await _workTypes.GetAsync(id));
    }
}
