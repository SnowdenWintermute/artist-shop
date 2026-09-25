using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkTypeRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly ArtworkTypeRepository _artworkTypes = new(database.Site);
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly VocabularyRepository _vocabularies = new(database.Site);

    // test classes share the database in parallel, so names must not collide
    private static ArtworkTypeName UniqueName() => new($"Type {Guid.NewGuid():n}");

    // also checks the ArtworkField enum values line up with the seeded ids
    [Fact]
    public async Task ListsTheSeededFieldsPerType()
    {
        var photograph = await _artworkTypes.GetAsync(await _catalog.GetTypeIdAsync("Photograph"));
        var sculpture = await _artworkTypes.GetAsync(await _catalog.GetTypeIdAsync("Sculpture"));

        Assert.NotNull(photograph);
        Assert.NotNull(sculpture);
        Assert.Equal([ArtworkField.DateCreated, ArtworkField.HeightAndWidth], photograph.Fields);
        Assert.Equal(
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth],
            sculpture.Fields
        );
    }

    [Fact]
    public async Task AddedTypeHasItsNameAndFields()
    {
        var name = UniqueName();

        var id = await _artworkTypes.AddAsync(
            name,
            [ArtworkField.Depth, ArtworkField.HeightAndWidth, ArtworkField.Duration]
        );

        var artworkType = await _artworkTypes.GetAsync(id);
        Assert.NotNull(artworkType);
        Assert.Equal(name, artworkType.Name);
        Assert.Equal(
            [ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration],
            artworkType.Fields
        );
        Assert.Contains(new ArtworkType(id, name), await _artworkTypes.GetAllAsync());
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = UniqueName();
        await _artworkTypes.AddAsync(name, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _artworkTypes.AddAsync(name, [])
        );
    }

    [Fact]
    public async Task RefusesDepthWithoutHeightAndWidth()
    {
        await Assert.ThrowsAsync<PostgresException>(() =>
            _artworkTypes.AddAsync(UniqueName(), [ArtworkField.Depth])
        );
    }

    [Fact]
    public async Task GetReturnsNullForUnknownId()
    {
        // ids start at 1
        Assert.Null(await _artworkTypes.GetAsync(new ArtworkTypeId(0)));
    }

    [Fact]
    public async Task UpdateRenamesAndSwitchesFields()
    {
        var id = await _artworkTypes.AddAsync(
            UniqueName(),
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth]
        );
        var newName = UniqueName();

        await _artworkTypes.UpdateAsync(id, newName, [ArtworkField.Duration]);

        var artworkType = await _artworkTypes.GetAsync(id);
        Assert.NotNull(artworkType);
        Assert.Equal(newName, artworkType.Name);
        Assert.Equal([ArtworkField.Duration], artworkType.Fields);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateName()
    {
        var takenName = UniqueName();
        await _artworkTypes.AddAsync(takenName, []);
        var id = await _artworkTypes.AddAsync(UniqueName(), []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _artworkTypes.UpdateAsync(id, takenName, [])
        );
    }

    [Fact]
    public async Task UpdateOfDeletedTypeThrowsChangedSincePageLoad()
    {
        var id = await _artworkTypes.AddAsync(UniqueName(), []);
        await _artworkTypes.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _artworkTypes.UpdateAsync(id, UniqueName(), [])
        );
    }

    [Fact]
    public async Task SwitchingFieldsOffClearsTheirValues()
    {
        var id = await _artworkTypes.AddAsync(
            UniqueName(),
            [ArtworkField.HeightAndWidth, ArtworkField.Depth]
        );
        var identifiers = await _catalog.AddArtworkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m))
        );

        var counts = await _artworkTypes.CountArtworksAsync(id);
        Assert.Equal(new ArtworkTypeArtworkCounts(1, 0, 1, 1, 0), counts);

        await _artworkTypes.UpdateAsync(id, UniqueName(), [ArtworkField.DateCreated]);

        var artwork = await _artworks.GetByIdAsync(identifiers.Id);
        Assert.NotNull(artwork);
        Assert.Null(artwork.Dimensions);
    }

    [Fact]
    public async Task SwitchingDepthOffKeepsHeightAndWidth()
    {
        var id = await _artworkTypes.AddAsync(
            UniqueName(),
            [ArtworkField.HeightAndWidth, ArtworkField.Depth]
        );
        var identifiers = await _catalog.AddArtworkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m))
        );

        await _artworkTypes.UpdateAsync(id, UniqueName(), [ArtworkField.HeightAndWidth]);

        var artwork = await _artworks.GetByIdAsync(identifiers.Id);
        Assert.NotNull(artwork);
        Assert.Equal(
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null)),
            artwork.Dimensions
        );
    }

    [Fact]
    public async Task DeleteRemovesUnusedTypeAndItsVocabularyLinks()
    {
        var id = await _artworkTypes.AddAsync(UniqueName(), [ArtworkField.DateCreated]);
        var vocabularyId = await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            [id]
        );

        await _artworkTypes.DeleteAsync(id);

        Assert.Null(await _artworkTypes.GetAsync(id));
        var vocabulary = await _vocabularies.GetAsync(vocabularyId);
        Assert.NotNull(vocabulary);
        Assert.Empty(vocabulary.ArtworkTypeIds);
    }

    [Fact]
    public async Task DeleteRefusesTypeInUse()
    {
        var id = await _artworkTypes.AddAsync(UniqueName(), [ArtworkField.HeightAndWidth]);
        await _catalog.AddArtworkWithDimensionsAsync(
            id,
            new DimensionsCentimeters(new Dimensions(30m, 40m, depth: null))
        );

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _artworkTypes.DeleteAsync(id));
        Assert.NotNull(await _artworkTypes.GetAsync(id));
    }
}
