using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class WorkRepositoryTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly VocabularyTermRepository _terms = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly ProductTypeRepository _productTypes = new(database.Site);
    private readonly WorkImageRepository _images = new(database.Site);

    [Fact]
    public async Task NumbersTheSlugWhenItIsTaken()
    {
        var name = $"Sunset {Guid.NewGuid():n}";

        var first = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        var second = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        var third = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);

        Assert.Equal($"{first.Slug.Value}-2", second.Slug.Value);
        Assert.Equal($"{first.Slug.Value}-3", third.Slug.Value);
    }

    // each stands for a choice that changed while the add-work form was open
    [Fact]
    public async Task RejectsADeletedVocabularyTerm()
    {
        var termId = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        await _terms.DeleteAsync(termId);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale term {Guid.NewGuid():n}",
                termIds: [termId],
                collectionIds: [],
                images: []
            )
        );
    }

    [Fact]
    public async Task RejectsADeletedCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        await _collections.DeleteAsync(collectionId);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale collection {Guid.NewGuid():n}",
                termIds: [],
                collectionIds: [collectionId],
                images: []
            )
        );
    }

    [Fact]
    public async Task RejectsADeletedProductType()
    {
        var missingType = new ProductTypeId(int.MaxValue);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale product type {Guid.NewGuid():n}",
                termIds: [],
                collectionIds: [],
                images: [],
                products: [new ProductAddition(missingType, Label: null, 10m, EditionSize: null, 5)],
                duration: null
            )
        );
    }

    // the seeded painting type has no duration field
    [Fact]
    public async Task RejectsAValueForAFieldTheTypeDoesNotHave()
    {
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddPaintingAsync(
                $"Stale field {Guid.NewGuid():n}",
                termIds: [],
                collectionIds: [],
                images: [],
                products: [],
                duration: TimeSpan.FromMinutes(3)
            )
        );
    }

    [Fact]
    public async Task ReadsBackTheTypeAndProducts()
    {
        var productTypes = await _productTypes.GetAllAsync();
        var original = productTypes.Single(productType => productType.Name.Value == "Original");
        var print = productTypes.Single(productType => productType.Name.Value == "Print");

        var identifiers = await _catalog.AddPaintingAsync(
            $"With products {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: [],
            products:
            [
                new ProductAddition(original.Id, Label: null, Price: null, EditionSize: 1, Stock: 0),
                new ProductAddition(print.Id, "A4", 40m, EditionSize: null, Stock: 100),
            ],
            duration: null
        );

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal(await _catalog.GetPaintingTypeIdAsync(), work.Type.Id);
        Assert.Collection(
            work.Products,
            sold =>
            {
                Assert.Equal(original, sold.Type);
                Assert.Null(sold.Price);
                Assert.Equal(1, sold.EditionSize);
                Assert.Equal(0, sold.Stock);
            },
            openPrint =>
            {
                Assert.Equal(print, openPrint.Type);
                Assert.Equal("A4", openPrint.Label);
                Assert.Equal(40m, openPrint.Price);
                Assert.Null(openPrint.EditionSize);
                Assert.Equal(100, openPrint.Stock);
            }
        );
    }

    [Fact]
    public async Task FindsTheSameWorkBySlugAndById()
    {
        var identifiers = await _catalog.AddPaintingAsync(
            $"Lookup {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: []
        );

        var bySlug = await _works.GetBySlugAsync(identifiers.Slug.Value);

        Assert.NotNull(bySlug);
        Assert.Equal(identifiers.Id, bySlug.Id);
        Assert.Null(await _works.GetBySlugAsync($"missing-{Guid.NewGuid():n}"));
    }

    [Fact]
    public async Task RejectsDepthOnAPhotograph()
    {
        var photographTypeId = await _catalog.GetTypeIdAsync("Photograph");
        var withDepth = new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m));

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _catalog.AddWorkWithDimensionsAsync(photographTypeId, withDepth)
        );
    }

    [Fact]
    public async Task KeepsHeightWidthAndDepthApart()
    {
        var sculptureTypeId = await _catalog.GetTypeIdAsync("Sculpture");
        var dimensions = new DimensionsCentimeters(new Dimensions(30m, 40m, depth: 2m));

        var identifiers = await _catalog.AddWorkWithDimensionsAsync(sculptureTypeId, dimensions);
        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal(dimensions, work.Dimensions);
    }

    private async Task<WorkCatalogAddition> PaintingAdditionAsync(string name, IReadOnlyList<CollectionId> collectionIds) =>
        CatalogTestData.CreateWorkAddition(
            await _catalog.GetPaintingTypeIdAsync(),
            name,
            termIds: [],
            collectionIds,
            images: [],
            products: [],
            duration: null
        );

    [Fact]
    public async Task AddManyAddsEveryWork()
    {
        var first = await PaintingAdditionAsync($"Many first {Guid.NewGuid():n}", collectionIds: []);
        var second = await PaintingAdditionAsync($"Many second {Guid.NewGuid():n}", collectionIds: []);

        var identifiers = await _works.AddManyAsync([first, second]);

        Assert.Equal(
            [first.Name, second.Name],
            await Task.WhenAll(identifiers.Select(async added => (await _works.GetByIdAsync(added.Id))?.Name))
        );
    }

    // two collections in one insert is the case that would collide on Unique_Collections_SortOrder if they
    // each asked for MAX + 1
    [Fact]
    public async Task AddManyCreatesTheCollectionsTheWorksName()
    {
        var shared = new CollectionName($"Nocturnes {Guid.NewGuid():n}");
        var second = new CollectionName($"Gardens {Guid.NewGuid():n}");

        var first = (await PaintingAdditionAsync($"New collection first {Guid.NewGuid():n}", collectionIds: [])) with
        {
            NewCollectionNames = [shared, second],
        };
        var later = (await PaintingAdditionAsync($"New collection later {Guid.NewGuid():n}", collectionIds: [])) with
        {
            NewCollectionNames = [shared],
        };

        await _works.AddManyAsync([first, later]);

        var allCollections = await _collections.GetAllAsync();
        var createdShared = Assert.Single(allCollections, collection => collection.Name == shared);
        Assert.Single(allCollections, collection => collection.Name == second);

        var withWorks = await _collections.GetAsync(createdShared.Id);
        Assert.NotNull(withWorks);
        Assert.Equal([first.Name, later.Name], [.. withWorks.Works.Select(work => work.Name)]);
    }

    [Fact]
    public async Task AddManyRefusesACollectionNameAnotherCollectionTookSinceTheReview()
    {
        var name = new CollectionName($"Taken {Guid.NewGuid():n}");
        await _collections.AddAsync(name, CollectionSlug.FromName(name.Value));

        var addition = (await PaintingAdditionAsync($"Taken name {Guid.NewGuid():n}", collectionIds: [])) with
        {
            NewCollectionNames = [name],
        };

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _works.AddManyAsync([addition]));
        Assert.Null(await _works.GetBySlugAsync(addition.CandidateSlug.Value));
    }

    [Fact]
    public async Task AddManyRollsBackEarlierWorksWhenALaterOneFails()
    {
        var deletedCollectionId = await _catalog.AddCollectionAsync();
        await _collections.DeleteAsync(deletedCollectionId);
        var first = await PaintingAdditionAsync($"Rolled back {Guid.NewGuid():n}", collectionIds: []);
        var stale = await PaintingAdditionAsync($"Stale {Guid.NewGuid():n}", collectionIds: [deletedCollectionId]);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _works.AddManyAsync([first, stale]));

        Assert.Null(await _works.GetBySlugAsync(first.CandidateSlug.Value));
    }

    [Fact]
    public async Task DeleteManyDeletesOnlyTheGivenWorks()
    {
        var first = await _catalog.AddPaintingAsync($"Deleted {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);
        var second = await _catalog.AddPaintingAsync($"Deleted {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);
        var kept = await _catalog.AddPaintingAsync($"Kept {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);

        await _works.DeleteManyAsync([first.Id, second.Id]);

        Assert.Null(await _works.GetByIdAsync(first.Id));
        Assert.Null(await _works.GetByIdAsync(second.Id));
        Assert.NotNull(await _works.GetByIdAsync(kept.Id));
    }

    private static WorkCatalogUpdate UpdateOf(
        WorkId id,
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds
    ) => new(DetailsUpdateOf(id, name, termIds, collectionIds), Images: [], MainImageIndex: 0);

    private static WorkDetailsUpdate DetailsUpdateOf(
        WorkId id,
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds
    ) =>
        new(
            id,
            new WorkName(name),
            WorkSlug.FromName(name),
            Description: null,
            DateCreated: null,
            Dimensions: null,
            Duration: null,
            VocabularyTermIds: termIds,
            CollectionIds: collectionIds
        );

    // saving a form that nobody renamed must not walk sunset-2 along to sunset-3
    [Fact]
    public async Task KeepsANumberedSlugWhenTheNameIsUnchanged()
    {
        var name = $"Sunset {Guid.NewGuid():n}";
        await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        var numbered = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);

        var slug = await _works.UpdateAsync(UpdateOf(numbered.Id, name, termIds: [], collectionIds: []));

        Assert.Equal(numbered.Slug, slug);
    }

    [Fact]
    public async Task NumbersTheSlugWhenARenameLandsOnATakenOne()
    {
        var takenName = $"Harbour {Guid.NewGuid():n}";
        var taken = await _catalog.AddPaintingAsync(takenName, termIds: [], collectionIds: [], images: []);
        var renamed = await _catalog.AddPaintingAsync($"Moon {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);

        var slug = await _works.UpdateAsync(UpdateOf(renamed.Id, takenName, termIds: [], collectionIds: []));

        Assert.Equal($"{taken.Slug.Value}-2", slug.Value);
    }

    [Fact]
    public async Task ReplacesTermsAndCollections()
    {
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var oldTermId = await _catalog.AddTermAsync(vocabularyId);
        var newTermId = await _catalog.AddTermAsync(vocabularyId);
        var oldCollectionId = await _catalog.AddCollectionAsync();
        var newCollectionId = await _catalog.AddCollectionAsync();

        var name = $"Rechosen {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(name, [oldTermId], [oldCollectionId], images: []);

        await _works.UpdateAsync(UpdateOf(identifiers.Id, name, [newTermId], [newCollectionId]));

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal([newTermId], [.. work.VocabularyTerms.Select(term => term.Id)]);
        Assert.Equal([newCollectionId], [.. work.Collections.Select(collection => collection.Id)]);
    }

    // the artist dragged this work to the front of the collection; an unrelated save mustn't move it
    [Fact]
    public async Task LeavesAnWorkWhereItIsInACollectionItStaysIn()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var name = $"First in collection {Guid.NewGuid():n}";
        var staying = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [collectionId], images: []);
        var after = await _catalog.AddPaintingAsync(
            $"Second in collection {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        await _works.UpdateAsync(UpdateOf(staying.Id, name, termIds: [], collectionIds: [collectionId]));

        var withWorks = await _collections.GetAsync(collectionId);

        Assert.NotNull(withWorks);
        Assert.Equal([staying.Id, after.Id], [.. withWorks.Works.Select(work => work.Id)]);
    }

    [Fact]
    public async Task RejectsATermDeletedWhileTheEditFormWasOpen()
    {
        var termId = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        var name = $"Stale term on edit {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        await _terms.DeleteAsync(termId);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _works.UpdateAsync(UpdateOf(identifiers.Id, name, [termId], collectionIds: []))
        );
    }

    [Fact]
    public async Task RefusesToUpdateAnWorkThatWasDeleted()
    {
        var name = $"Gone {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        await _works.DeleteAsync(identifiers.Id);

        await Assert.ThrowsAsync<WorkDeletedException>(() =>
            _works.UpdateAsync(UpdateOf(identifiers.Id, name, termIds: [], collectionIds: []))
        );
    }

    // what a post's work embeds are drawn from: the image, its work, and where it sits
    [Fact]
    public async Task FindsImagesByStorageKeyWithTheirWorkAndPlace()
    {
        var first = CatalogTestData.CreateTestImage();
        var second = CatalogTestData.CreateTestImage();
        var identifiers = await _catalog.AddPaintingAsync(
            $"Embedded {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: [first, second]
        );
        var deletedKey = CatalogTestData.CreateTestImage().StorageKey;

        var found = await _works.GetImagesByStorageKeyAsync([second.StorageKey, deletedKey]);

        var source = Assert.Single(found).Value;
        Assert.Equal(identifiers.Id, source.WorkId);
        Assert.Equal(identifiers.Slug, source.Work.Slug);
        Assert.Equal(second.StorageKey, source.Image.StorageKey);
        Assert.Equal(2, source.ImageNumber);
    }

    // the image rows go through ON DELETE CASCADE, which is what frees the files for the sweep
    [Fact]
    public async Task DeleteTakesTheWorksImagesWithIt()
    {
        var image = CatalogTestData.CreateTestImage();
        var collectionId = await _catalog.AddCollectionAsync();
        var identifiers = await _catalog.AddPaintingAsync(
            $"Deleted {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: [image]
        );

        await _works.DeleteAsync(identifiers.Id);

        Assert.Null(await _works.GetByIdAsync(identifiers.Id));
        Assert.DoesNotContain(image.StorageKey, await _images.GetAllStorageKeysAsync());

        var withWorks = await _collections.GetAsync(collectionId);
        Assert.NotNull(withWorks);
        Assert.Empty(withWorks.Works);
    }

    // the form posts the whole list, so a save is where an image is added, moved or taken away
    [Fact]
    public async Task ReplacesTheImagesWithWhatWasPosted()
    {
        var kept = CatalogTestData.CreateTestImage();
        var removed = CatalogTestData.CreateTestImage();
        var name = $"Reimaged {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(
            name,
            termIds: [],
            collectionIds: [],
            images: [kept, removed]
        );

        var added = CatalogTestData.CreateTestImage();
        var update = UpdateOf(identifiers.Id, name, termIds: [], collectionIds: []) with
        {
            Images = [added, kept],
            MainImageIndex = 1,
        };

        await _works.UpdateAsync(update);

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal(
            [added.StorageKey, kept.StorageKey],
            [.. work.Images.Select(image => image.StorageKey)]
        );
        Assert.Equal(1, work.MainImageIndex);
        Assert.DoesNotContain(removed.StorageKey, await _images.GetAllStorageKeysAsync());
    }

    // a work with no images is a normal state: that is what the CSV import produces
    [Fact]
    public async Task TakesEveryImageAwayWhenNoneWerePosted()
    {
        var image = CatalogTestData.CreateTestImage();
        var name = $"Unimaged {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(
            name,
            termIds: [],
            collectionIds: [],
            images: [image]
        );

        await _works.UpdateAsync(UpdateOf(identifiers.Id, name, termIds: [], collectionIds: []));

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Empty(work.Images);
        Assert.DoesNotContain(image.StorageKey, await _images.GetAllStorageKeysAsync());
    }

    // the add-from-images table saves rows while their uploads are still appending images
    [Fact]
    public async Task UpdatingDetailsKeepsTheImages()
    {
        var image = CatalogTestData.CreateTestImage();
        var collectionId = await _catalog.AddCollectionAsync();
        var identifiers = await _catalog.AddPaintingAsync(
            $"Dropped {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: [image]
        );

        var renamed = $"Renamed {Guid.NewGuid():n}";
        var slug = await _works.UpdateDetailsAsync(
            DetailsUpdateOf(identifiers.Id, renamed, termIds: [], collectionIds: [collectionId]) with
            {
                Description = "Oil on board",
            }
        );

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal(renamed, work.Name.Value);
        Assert.Equal(slug, work.Slug);
        Assert.Equal("Oil on board", work.Description);
        Assert.Equal([collectionId], [.. work.Collections.Select(collection => collection.Id)]);
        Assert.Equal([image.StorageKey], [.. work.Images.Select(kept => kept.StorageKey)]);
    }

    [Fact]
    public async Task RefusesToUpdateTheDetailsOfAnWorkThatWasDeleted()
    {
        var name = $"Gone before saving {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [], images: []);
        await _works.DeleteAsync(identifiers.Id);

        await Assert.ThrowsAsync<WorkDeletedException>(() =>
            _works.UpdateDetailsAsync(DetailsUpdateOf(identifiers.Id, name, termIds: [], collectionIds: []))
        );
    }

    [Fact]
    public async Task GetManyReadsOnlyTheWorksAskedFor()
    {
        var termId = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        var collectionId = await _catalog.AddCollectionAsync();
        var image = CatalogTestData.CreateTestImage();
        var first = await _catalog.AddPaintingAsync($"Asked {Guid.NewGuid():n}", termIds: [termId], collectionIds: [collectionId], images: [image]);
        var second = await _catalog.AddPaintingAsync($"Asked {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);
        var other = await _catalog.AddPaintingAsync($"Not asked {Guid.NewGuid():n}", termIds: [termId], collectionIds: [], images: []);

        var works = await _works.GetManyAsync([second.Id, first.Id]);

        Assert.Equal([first.Id, second.Id], [.. works.Select(work => work.Id)]);
        Assert.DoesNotContain(other.Id, works.Select(work => work.Id));
        Assert.Equal([image.StorageKey], [.. works[0].Images.Select(read => read.StorageKey)]);
        Assert.Equal([termId], [.. works[0].VocabularyTerms.Select(term => term.Id)]);
        Assert.Equal([collectionId], [.. works[0].Collections.Select(collection => collection.Id)]);
        Assert.Empty(works[1].VocabularyTerms);
    }

    [Fact]
    public async Task UpdatingImagesKeepsTheDetails()
    {
        var kept = CatalogTestData.CreateTestImage();
        var removed = CatalogTestData.CreateTestImage();
        var collectionId = await _catalog.AddCollectionAsync();
        var name = $"Images only {Guid.NewGuid():n}";
        var identifiers = await _catalog.AddPaintingAsync(name, termIds: [], collectionIds: [collectionId], images: [kept, removed]);

        var added = CatalogTestData.CreateTestImage();
        await _works.UpdateImagesAsync(identifiers.Id, [added, kept], mainImageIndex: 1);

        var work = await _works.GetByIdAsync(identifiers.Id);

        Assert.NotNull(work);
        Assert.Equal(name, work.Name.Value);
        Assert.Equal([collectionId], [.. work.Collections.Select(collection => collection.Id)]);
        Assert.Equal([added.StorageKey, kept.StorageKey], [.. work.Images.Select(image => image.StorageKey)]);
        Assert.Equal(1, work.MainImageIndex);
    }

    [Fact]
    public async Task RefusesToUpdateTheImagesOfAnWorkThatWasDeleted()
    {
        var identifiers = await _catalog.AddPaintingAsync($"Gone {Guid.NewGuid():n}", termIds: [], collectionIds: [], images: []);
        await _works.DeleteAsync(identifiers.Id);

        await Assert.ThrowsAsync<WorkDeletedException>(() =>
            _works.UpdateImagesAsync(identifiers.Id, [CatalogTestData.CreateTestImage()], mainImageIndex: 0)
        );
    }
}
