using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class VocabularyRepositoryTests(TestDatabaseFixture database)
{
    private readonly VocabularyRepository _vocabularies = new(database.Site);
    private readonly ArtworkTypeRepository _artworkTypes = new(database.Site);
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    // test classes share the database in parallel, so names must not collide
    private static VocabularyName UniqueName() => new($"Medium {Guid.NewGuid():n}");

    // the artwork list filters across every work type at once, so this one is not scoped to one
    [Fact]
    public async Task ListsEveryVocabularyWithItsTerms()
    {
        var withTerms = await _catalog.AddPaintingVocabularyAsync();
        var firstTerm = await _catalog.AddTermAsync(withTerms);
        var secondTerm = await _catalog.AddTermAsync(withTerms);
        var withoutTerms = await _catalog.AddPaintingVocabularyAsync();

        var vocabularies = await _vocabularies.GetAllWithTermsAsync();

        var listed = vocabularies.Single(vocabulary => vocabulary.Id == withTerms);
        Assert.Equal(
            [firstTerm, secondTerm],
            [.. listed.Terms.Select(term => term.Id).OrderBy(id => id.Value)]
        );
        Assert.Empty(vocabularies.Single(vocabulary => vocabulary.Id == withoutTerms).Terms);
    }

    [Fact]
    public async Task ListsTheSeededPaintingType()
    {
        var artworkTypes = await _artworkTypes.GetAllAsync();

        Assert.Contains(artworkTypes, artworkType => artworkType.Name.Value == "Painting");
    }

    [Fact]
    public async Task AddedVocabularyAppearsInTheList()
    {
        var name = UniqueName();

        var id = await _vocabularies.AddAsync(name, isMutuallyExclusive: false, [await _catalog.GetPaintingTypeIdAsync()]);

        Assert.Contains(new Vocabulary(id, name), await _vocabularies.GetAllAsync());
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = UniqueName();
        await _vocabularies.AddAsync(name, isMutuallyExclusive: false, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _vocabularies.AddAsync(name, isMutuallyExclusive: false, []));
    }

    [Fact]
    public async Task GetReturnsVocabularNameAndAssociatedArtworkTypes()
    {
        var name = UniqueName();
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, isMutuallyExclusive: false, [paintingTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);

        Assert.NotNull(vocabulary);
        Assert.Equal(name, vocabulary.Name);
        Assert.Equal(paintingTypeId, Assert.Single(vocabulary.ArtworkTypeIds));
    }

    [Fact]
    public async Task GetReturnsNullForUnknownId()
    {
        // ids start at 1
        Assert.Null(await _vocabularies.GetAsync(new VocabularyId(0)));
    }

    [Fact]
    public async Task UpdateRenames()
    {
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(UniqueName(), isMutuallyExclusive: false, [paintingTypeId]);
        var newName = UniqueName();

        await _vocabularies.UpdateAsync(id, newName, isMutuallyExclusive: false, [paintingTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Equal(newName, vocabulary.Name);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateName()
    {
        var takenName = UniqueName();
        await _vocabularies.AddAsync(takenName, isMutuallyExclusive: false, []);
        var id = await _vocabularies.AddAsync(UniqueName(), isMutuallyExclusive: false, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _vocabularies.UpdateAsync(id, takenName, isMutuallyExclusive: false, [])
        );
    }

    [Fact]
    public async Task UncheckingArtworkTypeRemovesVocabularyTermsFromItsItemsButKeepsTerms()
    {
        var name = UniqueName();
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, isMutuallyExclusive: false, [paintingTypeId]);
        await _catalog.AddPaintingWithTermAsync(await _catalog.AddTermAsync(id));
        Assert.Equal(1, (await _vocabularies.CountUsageAsync(id)).ArtworkCountFor(paintingTypeId));

        await _vocabularies.UpdateAsync(id, name, isMutuallyExclusive: false, []);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Empty(vocabulary.ArtworkTypeIds);
        var usage = await _vocabularies.CountUsageAsync(id);
        Assert.Equal(0, usage.ArtworkCountFor(paintingTypeId));
        Assert.Equal(1, usage.VocabularyTermCount);
    }

    [Fact]
    public async Task DeleteRemovesVocabularyAndKeepsArtworks()
    {
        var id = await _vocabularies.AddAsync(
            UniqueName(),
            isMutuallyExclusive: false,
            [await _catalog.GetPaintingTypeIdAsync()]
        );
        var slug = await _catalog.AddPaintingWithTermAsync(await _catalog.AddTermAsync(id));

        await _vocabularies.DeleteAsync(id);

        Assert.Null(await _vocabularies.GetAsync(id));
        var painting = await _artworks.GetBySlugAsync(slug.Value);
        Assert.NotNull(painting);
        Assert.Empty(painting.VocabularyTerms);
    }

    [Fact]
    public async Task ListsOnlyVocabulariesThatApplyToNoArtworkType()
    {
        var withoutTypesId = await _vocabularies.AddAsync(UniqueName(), isMutuallyExclusive: false, []);
        var forPaintingsId = await _catalog.AddPaintingVocabularyAsync();

        var vocabularies = await _vocabularies.GetAllWithoutArtworkTypesAsync();

        Assert.Contains(vocabularies, vocabulary => vocabulary.Id == withoutTypesId);
        Assert.DoesNotContain(vocabularies, vocabulary => vocabulary.Id == forPaintingsId);
    }

    [Fact]
    public async Task UpdateRejectsADeletedVocabulary()
    {
        var id = await _catalog.AddPaintingVocabularyAsync();
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        await _vocabularies.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _vocabularies.UpdateAsync(id, UniqueName(), isMutuallyExclusive: false, [paintingTypeId])
        );
    }

    [Fact]
    public async Task ListsVocabulariesForAArtworkTypeWithTheirTerms()
    {
        var withTermsId = await _catalog.AddPaintingVocabularyAsync();
        var termId = await _catalog.AddTermAsync(withTermsId);
        var withoutTermsId = await _catalog.AddPaintingVocabularyAsync();
        var notForPaintingsId = await _vocabularies.AddAsync(UniqueName(), isMutuallyExclusive: false, []);

        var vocabularies = await _vocabularies.GetAllWithTermsForArtworkTypeAsync(
            await _catalog.GetPaintingTypeIdAsync()
        );

        Assert.Equal([termId], vocabularies.Single(vocabulary => vocabulary.Id == withTermsId).Terms.Select(term => term.Id));
        Assert.Empty(vocabularies.Single(vocabulary => vocabulary.Id == withoutTermsId).Terms);
        Assert.DoesNotContain(vocabularies, vocabulary => vocabulary.Id == notForPaintingsId);
    }

    // the type was deleted in another tab while the vocabulary form was open
    [Fact]
    public async Task AddAndUpdateSkipADeletedArtworkType()
    {
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var deletedTypeId = await _artworkTypes.AddAsync(
            new ArtworkTypeName($"Type {Guid.NewGuid():n}"),
            []
        );
        await _artworkTypes.DeleteAsync(deletedTypeId);

        var id = await _vocabularies.AddAsync(UniqueName(), isMutuallyExclusive: false, [paintingTypeId, deletedTypeId]);
        await _vocabularies.UpdateAsync(id, UniqueName(), isMutuallyExclusive: false, [paintingTypeId, deletedTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Equal([paintingTypeId], vocabulary.ArtworkTypeIds);
    }

    [Fact]
    public async Task AddOrLinkAddsAVocabularyForTheType()
    {
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var name = UniqueName();

        var id = await _vocabularies.AddOrLinkAsync(name, paintingTypeId);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Equal(name, vocabulary.Name);
        Assert.Equal([paintingTypeId], vocabulary.ArtworkTypeIds);
    }

    [Fact]
    public async Task MakingMutuallyExclusiveRemovesAllItsTermsOnlyFromArtworksWithSeveral()
    {
        var name = UniqueName();
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, isMutuallyExclusive: false, [paintingTypeId]);
        var noon = await _catalog.AddTermAsync(id);
        var midnight = await _catalog.AddTermAsync(id);
        var bothName = $"Both {Guid.NewGuid():n}";
        await _catalog.AddPaintingAsync(bothName, termIds: [noon, midnight], seriesIds: [], images: []);
        await _catalog.AddPaintingWithTermAsync(noon);

        Assert.Equal([new ArtworkName(bothName)], await _vocabularies.GetArtworksWithSeveralTermsAsync(id));

        await _vocabularies.UpdateAsync(id, name, isMutuallyExclusive: true, [paintingTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.True(vocabulary.IsMutuallyExclusive);
        Assert.Equal(1, (await _vocabularies.CountUsageAsync(id)).ArtworkCountFor(paintingTypeId));
        Assert.Empty(await _vocabularies.GetArtworksWithSeveralTermsAsync(id));
    }

    [Fact]
    public async Task ArtworkCantHaveTwoTermsOfAMutuallyExclusiveVocabulary()
    {
        var id = await _vocabularies.AddAsync(
            UniqueName(),
            isMutuallyExclusive: true,
            [await _catalog.GetPaintingTypeIdAsync()]
        );
        var noon = await _catalog.AddTermAsync(id);
        var midnight = await _catalog.AddTermAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddPaintingAsync($"Both {Guid.NewGuid():n}", termIds: [noon, midnight], seriesIds: [], images: [])
        );
    }

    [Fact]
    public async Task MutuallyExclusiveVocabularyStillAllowsOneTermPerArtwork()
    {
        var name = UniqueName();
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, isMutuallyExclusive: true, [paintingTypeId]);
        await _catalog.AddPaintingWithTermAsync(await _catalog.AddTermAsync(id));
        await _catalog.AddPaintingWithTermAsync(await _catalog.AddTermAsync(id));

        // switching back and forth keeps the terms
        await _vocabularies.UpdateAsync(id, name, isMutuallyExclusive: false, [paintingTypeId]);
        await _vocabularies.UpdateAsync(id, name, isMutuallyExclusive: true, [paintingTypeId]);

        Assert.Equal(2, (await _vocabularies.CountUsageAsync(id)).ArtworkCountFor(paintingTypeId));
    }

    // the artist typing a name another type's vocabulary has, in any capitals
    [Fact]
    public async Task AddOrLinkGivesTheTypeToAVocabularyWithThatName()
    {
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var name = UniqueName();
        var existing = await _vocabularies.AddAsync(name, isMutuallyExclusive: false, []);

        var id = await _vocabularies.AddOrLinkAsync(new VocabularyName(name.Value.ToUpperInvariant()), paintingTypeId);
        var again = await _vocabularies.AddOrLinkAsync(name, paintingTypeId);

        Assert.Equal(existing, id);
        Assert.Equal(existing, again);
        var vocabulary = await _vocabularies.GetAsync(existing);
        Assert.NotNull(vocabulary);
        Assert.Equal(name, vocabulary.Name);
        Assert.Equal([paintingTypeId], vocabulary.ArtworkTypeIds);
    }
}
