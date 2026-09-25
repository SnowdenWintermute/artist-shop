using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class VocabularyTermRepositoryTests(TestDatabaseFixture database)
{
    private readonly VocabularyTermRepository _terms = new(database.Site);
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    [Fact]
    public async Task ListsVocabularyTermsWithHowManyArtworksUseThem()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var usedTermId = await _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"));
        var unusedTermId = await _terms.AddAsync(vocabularyId, new VocabularyTermName("Acrylic"));
        await _catalog.AddPaintingWithTermAsync(usedTermId);

        var terms = await _terms.GetAllWithUsageAsync(vocabularyId);

        Assert.Equal(2, terms.Count);
        Assert.Equal(1, terms.Single(term => term.Id == usedTermId).ArtworkCount);
        Assert.Equal(0, terms.Single(term => term.Id == unusedTermId).ArtworkCount);
    }

    [Fact]
    public async Task RejectsDuplicateNameInSameVocabulary()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        await _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"));

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"))
        );
    }

    [Fact]
    public async Task AllowsSameNameInDifferentVocabularies()
    {
        var mediumId = await _catalog.AddPaintingVocabularyAsync();
        var supportId = await _catalog.AddPaintingVocabularyAsync();
        await _terms.AddAsync(mediumId, new VocabularyTermName("Paper"));

        await _terms.AddAsync(supportId, new VocabularyTermName("Paper"));

        Assert.Single(await _terms.GetAllWithUsageAsync(supportId));
    }

    [Fact]
    public async Task RenameChangesName()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var termId = await _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"));

        await _terms.RenameAsync(termId, new VocabularyTermName("Oil paint"));

        var term = Assert.Single(await _terms.GetAllWithUsageAsync(vocabularyId));
        Assert.Equal(new VocabularyTermName("Oil paint"), term.Name);
    }

    [Fact]
    public async Task RenameRejectsDuplicateName()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        await _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"));
        var termId = await _terms.AddAsync(vocabularyId, new VocabularyTermName("Acrylic"));

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _terms.RenameAsync(termId, new VocabularyTermName("Oil"))
        );
    }

    [Fact]
    public async Task DeleteRemovesTermFromItemsAndKeepsItems()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var termId = await _terms.AddAsync(vocabularyId, new VocabularyTermName("Oil"));
        var slug = await _catalog.AddPaintingWithTermAsync(termId);

        await _terms.DeleteAsync(termId);

        Assert.Empty(await _terms.GetAllWithUsageAsync(vocabularyId));
        var painting = await _artworks.GetBySlugAsync(slug.Value);
        Assert.NotNull(painting);
        Assert.Empty(painting.VocabularyTerms);
    }

    [Fact]
    public async Task RenameRejectsADeletedTerm()
    {
        var termId = await _terms.AddAsync(
            await _catalog.AddPaintingVocabularyAsync(),
            new VocabularyTermName("Oil")
        );
        await _terms.DeleteAsync(termId);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _terms.RenameAsync(termId, new VocabularyTermName("Oil paint"))
        );
    }
}
