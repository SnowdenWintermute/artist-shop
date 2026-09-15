using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

public sealed class PaintingRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);
    private readonly SeriesRepository _series = new(database.ConnectionFactory);
    private readonly VocabularyTermRepository _terms = new(database.ConnectionFactory);

    [Fact]
    public async Task NumbersTheSlugWhenItIsTaken()
    {
        var name = $"Sunset {Guid.NewGuid():n}";

        var first = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);
        var second = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);
        var third = await _catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);

        Assert.Equal($"{first.Slug.Value}-2", second.Slug.Value);
        Assert.Equal($"{first.Slug.Value}-3", third.Slug.Value);
    }

    // both stand for a choice deleted while the add-painting form was open
    [Fact]
    public async Task RejectsADeletedVocabularyTerm()
    {
        var termId = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        await _terms.DeleteAsync(termId);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale term {Guid.NewGuid():n}",
                termIds: [termId],
                seriesIds: [],
                images: []
            )
        );
    }

    [Fact]
    public async Task RejectsADeletedSeries()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        await _series.DeleteAsync(seriesId);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale series {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [seriesId],
                images: []
            )
        );
    }
}
