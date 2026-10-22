using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

// the work table's Combine: the kept work takes the others' images, collections and terms, then they go
[Collection(DatabaseCollection.Name)]
public sealed class WorkCombineTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly VocabularyRepository _vocabularies = new(database.Site);
    private readonly WorkTypeRepository _workTypes = new(database.Site);
    private readonly CustomPageRepository _pages = new(database.Site);
    private readonly PageRowRepository _rows = new(database.Site);
    private readonly SlideRepository _slides = new(database.Site);

    private static string UniqueName() => $"Combine test {Guid.NewGuid():n}";

    private Task<WorkIdentifiers> AddPaintingAsync(
        IReadOnlyList<WorkImage> images,
        IReadOnlyList<VocabularyTermId>? termIds = null,
        IReadOnlyList<CollectionId>? collectionIds = null
    ) => _catalog.AddPaintingAsync(UniqueName(), termIds ?? [], collectionIds ?? [], images);

    private async Task<Work> GetExistingAsync(WorkId id) => Unwrap.Value(await _works.GetByIdAsync(id));

    [Fact]
    public async Task TheOthersImagesFollowTheKeptWorksOwnAndTheOthersAreDeleted()
    {
        var keptImages = new[] { CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage() };
        var firstImages = new[] { CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage() };
        var secondImage = CatalogTestData.CreateTestImage();
        var kept = await AddPaintingAsync(keptImages);
        var first = await AddPaintingAsync(firstImages);
        var second = await AddPaintingAsync([secondImage]);

        // the order given, not id order
        await _works.CombineAsync(kept.Id, [second.Id, first.Id]);

        var work = await GetExistingAsync(kept.Id);
        Assert.Equal(
            [keptImages[0].StorageKey, keptImages[1].StorageKey, secondImage.StorageKey, firstImages[0].StorageKey, firstImages[1].StorageKey],
            work.Images.Select(image => image.StorageKey)
        );
        Assert.Equal(0, work.MainImageIndex);
        Assert.Null(await _works.GetByIdAsync(first.Id));
        Assert.Null(await _works.GetByIdAsync(second.Id));
    }

    [Fact]
    public async Task AKeptWorkWithNoImagesTakesTheFirstMovedOneAsItsMain()
    {
        var kept = await AddPaintingAsync([]);
        var other = await AddPaintingAsync([CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage()]);

        await _works.CombineAsync(kept.Id, [other.Id]);

        var work = await GetExistingAsync(kept.Id);
        Assert.Equal(2, work.Images.Count);
        Assert.Equal(0, work.MainImageIndex);
    }

    [Fact]
    public async Task TermsMergeAndAMutuallyExclusiveVocabularyKeepsTheKeptWorksTerm()
    {
        var typeId = await _catalog.GetPaintingTypeIdAsync();
        var openVocabulary = await _catalog.AddPaintingVocabularyAsync();
        var exclusiveVocabulary = await _vocabularies.AddAsync(new VocabularyName(UniqueName()), isMutuallyExclusive: true, [typeId]);
        var shared = await _catalog.AddTermAsync(openVocabulary);
        var othersOnly = await _catalog.AddTermAsync(openVocabulary);
        var keptExclusive = await _catalog.AddTermAsync(exclusiveVocabulary);
        var otherExclusive = await _catalog.AddTermAsync(exclusiveVocabulary);
        var kept = await AddPaintingAsync([], termIds: [shared, keptExclusive]);
        var other = await AddPaintingAsync([], termIds: [shared, othersOnly, otherExclusive]);

        await _works.CombineAsync(kept.Id, [other.Id]);

        Assert.Equal(
            new HashSet<VocabularyTermId> { shared, othersOnly, keptExclusive },
            (await GetExistingAsync(kept.Id)).VocabularyTerms.Select(term => term.Id).ToHashSet()
        );
    }

    [Fact]
    public async Task WithNoExclusiveTermOfItsOwnTheKeptWorkTakesTheFirstOthers()
    {
        var exclusiveVocabulary = await _vocabularies.AddAsync(
            new VocabularyName(UniqueName()),
            isMutuallyExclusive: true,
            [await _catalog.GetPaintingTypeIdAsync()]
        );
        var firstTerm = await _catalog.AddTermAsync(exclusiveVocabulary);
        var secondTerm = await _catalog.AddTermAsync(exclusiveVocabulary);
        var kept = await AddPaintingAsync([]);
        var second = await AddPaintingAsync([], termIds: [secondTerm]);
        var first = await AddPaintingAsync([], termIds: [firstTerm]);

        await _works.CombineAsync(kept.Id, [first.Id, second.Id]);

        Assert.Equal([firstTerm], (await GetExistingAsync(kept.Id)).VocabularyTerms.Select(term => term.Id));
    }

    [Fact]
    public async Task TheKeptWorkTakesTheFirstOthersPlaceInACollectionAndACoverAnotherHeld()
    {
        var keptIn = await _catalog.AddCollectionAsync();
        var othersIn = await _catalog.AddCollectionAsync();
        var keptImage = CatalogTestData.CreateTestImage();
        var kept = await AddPaintingAsync([keptImage], collectionIds: [keptIn]);
        var neighbour = await _catalog.AddPaintingInCollectionAsync(othersIn, [CatalogTestData.CreateTestImage()]);
        var other = await AddPaintingAsync([CatalogTestData.CreateTestImage()], collectionIds: [othersIn, keptIn]);
        var last = await _catalog.AddPaintingInCollectionAsync(othersIn, [CatalogTestData.CreateTestImage()]);
        await _collections.SetCoverAsync(keptIn, other.Id);

        await _works.CombineAsync(kept.Id, [other.Id]);

        Assert.Equal(
            [neighbour.Id, kept.Id, last.Id],
            Unwrap.Value(await _collections.GetAsync(othersIn)).Works.Select(work => work.Id)
        );
        var cover = (await _collections.GetAllWithCoversAsync()).Single(collection => collection.Id == keptIn).Cover;
        Assert.Equal(keptImage, cover);
    }

    [Fact]
    public async Task WhereSeveralOthersShareACollectionTheKeptWorkTakesTheFirstPlaceAndTheRestGo()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var kept = await AddPaintingAsync([CatalogTestData.CreateTestImage()]);
        var neighbour = await _catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var earlier = await _catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var last = await _catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var later = await _catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);

        // the place taken is the collection's first, not the first given
        await _works.CombineAsync(kept.Id, [later.Id, earlier.Id]);

        Assert.Equal(
            [neighbour.Id, kept.Id, last.Id],
            Unwrap.Value(await _collections.GetAsync(collectionId)).Works.Select(work => work.Id)
        );
    }

    [Fact]
    public async Task SlidesAndRowBackgroundsShowingTheOthersShowTheSamePictureFromTheKeptWork()
    {
        var kept = await AddPaintingAsync([CatalogTestData.CreateTestImage()]);
        var othersMain = CatalogTestData.CreateTestImage();
        var othersSecond = CatalogTestData.CreateTestImage();
        var other = await AddPaintingAsync([othersMain, othersSecond]);
        var name = UniqueName();
        var pageId = await _pages.AddAsync(name, CustomPageAddress.FromName(name));
        var rowId = await _rows.AddAsync(pageId);
        var owner = new SlideOwner.Row(rowId);
        await _slides.AddAsync(owner, new ImageSource.Work(other.Id, StorageKey: null));
        await _slides.AddAsync(owner, new ImageSource.Work(other.Id, othersSecond.StorageKey));
        var row = Assert.Single((await _pages.GetLayoutAsync(pageId)).Rows);
        await _rows.SetSettingsAsync(rowId, row.Settings with
        {
            Background = new RowBackground.Image(new ImageSource.Work(other.Id, StorageKey: null), ImageFit.Cover, Behind: null),
        });

        await _works.CombineAsync(kept.Id, [other.Id]);

        Assert.Equal(
            [new ImageSource.Work(kept.Id, othersMain.StorageKey), new ImageSource.Work(kept.Id, othersSecond.StorageKey)],
            (await _slides.GetAsync(owner)).Select(slide => slide.Source)
        );
        Assert.Equal(
            new RowBackground.Image(new ImageSource.Work(kept.Id, othersMain.StorageKey), ImageFit.Cover, Behind: null),
            Assert.Single((await _pages.GetLayoutAsync(pageId)).Rows).Background
        );
    }

    [Fact]
    public async Task ADeletedKeptWorkLeavesTheOthersAsTheyWere()
    {
        var kept = await AddPaintingAsync([]);
        var other = await AddPaintingAsync([CatalogTestData.CreateTestImage()]);
        await _works.DeleteAsync(kept.Id);

        await Assert.ThrowsAsync<WorkDeletedException>(() => _works.CombineAsync(kept.Id, [other.Id]));

        Assert.Single((await GetExistingAsync(other.Id)).Images);
    }

    [Fact]
    public async Task WorksOfAnotherTypeAreRefused()
    {
        var kept = await AddPaintingAsync([]);
        var typeId = await _workTypes.AddAsync(new WorkTypeName(UniqueName()), []);
        var other = await _catalog.AddWorkAsync(typeId, UniqueName());

        await Assert.ThrowsAsync<PostgresException>(() => _works.CombineAsync(kept.Id, [other.Id]));

        Assert.NotNull(await _works.GetByIdAsync(other.Id));
    }
}
