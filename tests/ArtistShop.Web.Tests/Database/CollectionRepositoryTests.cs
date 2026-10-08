using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class CollectionRepositoryTests(TestDatabaseFixture database)
{
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<CollectionId> AddNamedCollectionAsync(string name) =>
        _collections.AddAsync(new CollectionName(name), CollectionSlug.FromName(name));

    private async Task<CollectionWithWorks> GetExistingAsync(CollectionId id) =>
        await _collections.GetAsync(id) ?? throw new InvalidOperationException("The collection is missing.");

    // different names can share a slug when they differ only in what the slug drops:
    // punctuation, accents, or letter case
    [Fact]
    public async Task RejectsANameWhoseSlugAnotherCollectionHas()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedCollectionAsync(name);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => AddNamedCollectionAsync($"{name}!"));
    }

    [Fact]
    public async Task RenameRejectsANameWhoseSlugAnotherCollectionHas()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedCollectionAsync(name);
        var id = await _catalog.AddCollectionAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _collections.RenameAsync(id, new CollectionName($"{name}!"), CollectionSlug.FromName($"{name}!"))
        );
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        await AddNamedCollectionAsync(name);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => AddNamedCollectionAsync(name));
    }

    [Fact]
    public async Task NewCollectionGoesLast()
    {
        var id = await _catalog.AddCollectionAsync();

        Assert.Equal(id, (await _collections.GetAllWithCoversAsync()).Last().Id);
    }

    // reordering takes every collection, so these rely on nothing else adding one while they run.
    // Within a class xUnit runs one test at a time; across classes that is what DatabaseCollection is for
    [Fact]
    public async Task ReorderSetsTheOrderOfAllCollections()
    {
        await _catalog.AddCollectionAsync();
        await _catalog.AddCollectionAsync();
        List<CollectionId> reversed = [.. (await _collections.GetAllWithCoversAsync()).Select(collection => collection.Id).Reverse()];

        await _collections.ReorderAsync(reversed);

        Assert.Equal(reversed, (await _collections.GetAllWithCoversAsync()).Select(collection => collection.Id));
    }

    [Fact]
    public async Task GetAllListsCollectionsInTheArtistsOrder()
    {
        await _catalog.AddCollectionAsync();
        await _catalog.AddCollectionAsync();
        List<CollectionId> reversed = [.. (await _collections.GetAllWithCoversAsync()).Select(collection => collection.Id).Reverse()];
        await _collections.ReorderAsync(reversed);

        Assert.Equal(reversed, (await _collections.GetAllAsync()).Select(collection => collection.Id));
    }

    [Fact]
    public async Task WorkAddedToACollectionJoinsItLast()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        var second = await _catalog.AddPaintingInCollectionAsync(id, []);

        Assert.Equal([first.Id, second.Id], (await GetExistingAsync(id)).Works.Select(work => work.Id));
    }

    [Fact]
    public async Task ReorderRejectsAListMissingACollection()
    {
        await _catalog.AddCollectionAsync();
        List<CollectionId> allButOne = [.. (await _collections.GetAllWithCoversAsync()).Select(collection => collection.Id).Skip(1)];

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _collections.ReorderAsync(allButOne));
    }

    [Fact]
    public async Task RenameMovesTheSlugWithTheName()
    {
        var id = await _catalog.AddCollectionAsync();
        var newName = $"Green {Guid.NewGuid():n}";

        await _collections.RenameAsync(id, new CollectionName(newName), CollectionSlug.FromName(newName));

        var collection = await GetExistingAsync(id);
        Assert.Equal(new CollectionName(newName), collection.Name);
        Assert.Equal(CollectionSlug.FromName(newName), collection.Slug);
    }

    [Fact]
    public async Task RenameCanKeepItsOwnSlug()
    {
        var name = $"Blue {Guid.NewGuid():n}";
        var id = await AddNamedCollectionAsync(name);

        await _collections.RenameAsync(id, new CollectionName($"{name}!"), CollectionSlug.FromName($"{name}!"));

        Assert.Equal(CollectionSlug.FromName(name), (await GetExistingAsync(id)).Slug);
    }

    [Fact]
    public async Task ListsWorksInOrderWithTheirPrimaryImages()
    {
        var id = await _catalog.AddCollectionAsync();
        var image = CatalogTestData.CreateTestImage();
        var withImage = await _catalog.AddPaintingInCollectionAsync(id, [image]);
        var withoutImage = await _catalog.AddPaintingInCollectionAsync(id, []);

        var collection = await GetExistingAsync(id);

        Assert.Equal(
            [withImage.Id, withoutImage.Id],
            collection.Works.Select(work => work.Id)
        );
        Assert.Equal(image, collection.Works[0].PrimaryImage);
        Assert.Null(collection.Works[1].PrimaryImage);
        Assert.Equal(new WorkTypeName("Painting"), collection.Works[0].WorkTypeName);
    }

    [Fact]
    public async Task CoverFallsBackToTheFirstWorkWithAnImage()
    {
        var id = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(id, []);
        var image = CatalogTestData.CreateTestImage();
        await _catalog.AddPaintingInCollectionAsync(id, [image]);
        await _catalog.AddPaintingInCollectionAsync(id, [CatalogTestData.CreateTestImage()]);

        var collection = (await _collections.GetAllWithCoversAsync()).Single(collection => collection.Id == id);

        Assert.Equal(3, collection.WorkCount);
        Assert.Equal(image, collection.Cover);
    }

    [Fact]
    public async Task CollectionWithoutImagesHasNoCover()
    {
        var emptyId = await _catalog.AddCollectionAsync();
        var imagelessId = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(imagelessId, []);

        var allCollections = await _collections.GetAllWithCoversAsync();

        Assert.Null(allCollections.Single(collection => collection.Id == emptyId).Cover);
        Assert.Equal(0, allCollections.Single(collection => collection.Id == emptyId).WorkCount);
        Assert.Null(allCollections.Single(collection => collection.Id == imagelessId).Cover);
    }

    // what a visitor may see: a collection whose works have no photographs yet has nothing
    // to show, and the count has to mean the same thing as the page the card opens
    [Fact]
    public async Task VisibleCollectionsLeaveOutTheOnesWithNothingToLookAt()
    {
        var emptyId = await _catalog.AddCollectionAsync();
        var imagelessId = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(imagelessId, []);
        var photographedId = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(photographedId, [CatalogTestData.CreateTestImage()]);
        await _catalog.AddPaintingInCollectionAsync(photographedId, []);

        var visible = await _collections.GetVisibleWithCoversAsync();

        Assert.DoesNotContain(visible, collection => collection.Id == emptyId);
        Assert.DoesNotContain(visible, collection => collection.Id == imagelessId);
        Assert.Equal(1, visible.Single(collection => collection.Id == photographedId).WorkCount);
    }

    // the artist still has to see the collection they have yet to photograph
    [Fact]
    public async Task TheArtistsOwnListKeepsThem()
    {
        var imagelessId = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(imagelessId, []);

        var all = await _collections.GetAllWithCoversAsync();

        Assert.Equal(1, all.Single(collection => collection.Id == imagelessId).WorkCount);
    }

    [Fact]
    public async Task FindsACollectionByItsSlug()
    {
        var name = $"Harbour {Guid.NewGuid():n}";
        var id = await AddNamedCollectionAsync(name);

        var found = await _collections.GetBySlugAsync(CollectionSlug.FromName(name).Value);

        Assert.Equal(id, found?.Id);
        Assert.Null(await _collections.GetBySlugAsync($"missing-{Guid.NewGuid():n}"));
    }

    [Fact]
    public async Task FindsACollectionByItsId()
    {
        var name = $"Harbour {Guid.NewGuid():n}";
        var id = await AddNamedCollectionAsync(name);

        Assert.Equal(new CollectionName(name), (await _collections.GetByIdAsync(id))?.Name);

        await _collections.DeleteAsync(id);
        Assert.Null(await _collections.GetByIdAsync(id));
    }

    [Fact]
    public async Task StarredWorkIsTheCover()
    {
        var id = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(id, [CatalogTestData.CreateTestImage()]);
        var secondImage = CatalogTestData.CreateTestImage();
        var second = await _catalog.AddPaintingInCollectionAsync(id, [secondImage]);

        await _collections.SetCoverAsync(id, second.Id);

        var collection = (await _collections.GetAllWithCoversAsync()).Single(collection => collection.Id == id);
        Assert.Equal(secondImage, collection.Cover);
    }

    [Fact]
    public async Task MovingTheStarReplacesTheCover()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, [CatalogTestData.CreateTestImage()]);
        var secondImage = CatalogTestData.CreateTestImage();
        var second = await _catalog.AddPaintingInCollectionAsync(id, [secondImage]);
        await _collections.SetCoverAsync(id, first.Id);

        await _collections.SetCoverAsync(id, second.Id);

        var collection = (await _collections.GetAllWithCoversAsync()).Single(collection => collection.Id == id);
        Assert.Equal(secondImage, collection.Cover);
        Assert.Equal(
            [false, true],
            (await GetExistingAsync(id)).Works.Select(work => work.IsCover)
        );
    }

    [Fact]
    public async Task ClearingTheStarFallsBackToTheFirstWorkWithAnImage()
    {
        var id = await _catalog.AddCollectionAsync();
        var firstImage = CatalogTestData.CreateTestImage();
        await _catalog.AddPaintingInCollectionAsync(id, [firstImage]);
        var second = await _catalog.AddPaintingInCollectionAsync(id, [CatalogTestData.CreateTestImage()]);
        await _collections.SetCoverAsync(id, second.Id);

        await _collections.ClearCoverAsync(id);

        var collection = (await _collections.GetAllWithCoversAsync()).Single(collection => collection.Id == id);
        Assert.Equal(firstImage, collection.Cover);
        Assert.All((await GetExistingAsync(id)).Works, work => Assert.False(work.IsCover));
    }

    [Fact]
    public async Task RejectsACoverWithNoImage()
    {
        var id = await _catalog.AddCollectionAsync();
        var work = await _catalog.AddPaintingInCollectionAsync(id, []);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _collections.SetCoverAsync(id, work.Id));
    }

    [Fact]
    public async Task ReorderSwapsWorks()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        var second = await _catalog.AddPaintingInCollectionAsync(id, []);
        var third = await _catalog.AddPaintingInCollectionAsync(id, []);

        await _collections.ReorderWorksAsync(id, [third.Id, second.Id, first.Id]);

        Assert.Equal(
            [third.Id, second.Id, first.Id],
            (await GetExistingAsync(id)).Works.Select(work => work.Id)
        );
    }

    [Fact]
    public async Task ReorderRejectsAListThatDoesNotMatchTheCollection()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        await _catalog.AddPaintingInCollectionAsync(id, []);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _collections.ReorderWorksAsync(id, [first.Id])
        );
    }

    [Fact]
    public async Task RenameRejectsADeletedCollection()
    {
        var id = await _catalog.AddCollectionAsync();
        await _collections.DeleteAsync(id);
        var name = $"Renamed {Guid.NewGuid():n}";

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _collections.RenameAsync(id, new CollectionName(name), CollectionSlug.FromName(name))
        );
    }

    [Fact]
    public async Task RemovesOnlyTheChosenWorks()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        var second = await _catalog.AddPaintingInCollectionAsync(id, []);
        var third = await _catalog.AddPaintingInCollectionAsync(id, []);

        await _collections.RemoveWorksAsync(id, [second.Id]);

        Assert.Equal(
            [first.Id, third.Id],
            (await GetExistingAsync(id)).Works.Select(work => work.Id)
        );
    }

    [Fact]
    public async Task AddedWorksJoinTheEndInTheOrderGiven()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        var otherId = await _catalog.AddCollectionAsync();
        var second = await _catalog.AddPaintingInCollectionAsync(otherId, []);
        var third = await _catalog.AddPaintingInCollectionAsync(otherId, []);

        await _collections.AddWorksAsync(id, [third.Id, second.Id]);

        Assert.Equal(
            [first.Id, third.Id, second.Id],
            (await GetExistingAsync(id)).Works.Select(work => work.Id)
        );
    }

    // checked in a page loaded before another tab added it
    [Fact]
    public async Task AddingAnWorkAlreadyInTheCollectionKeepsItsPlace()
    {
        var id = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingInCollectionAsync(id, []);
        var second = await _catalog.AddPaintingInCollectionAsync(id, []);
        var otherId = await _catalog.AddCollectionAsync();
        var third = await _catalog.AddPaintingInCollectionAsync(otherId, []);

        await _collections.AddWorksAsync(id, [first.Id, third.Id]);

        Assert.Equal(
            [first.Id, second.Id, third.Id],
            (await GetExistingAsync(id)).Works.Select(work => work.Id)
        );
    }

    // checked in a page loaded before another tab deleted it
    [Fact]
    public async Task AddingSkipsADeletedWork()
    {
        var id = await _catalog.AddCollectionAsync();
        var otherId = await _catalog.AddCollectionAsync();
        var deleted = await _catalog.AddPaintingInCollectionAsync(otherId, []);
        var kept = await _catalog.AddPaintingInCollectionAsync(otherId, []);
        await _works.DeleteAsync(deleted.Id);

        await _collections.AddWorksAsync(id, [deleted.Id, kept.Id]);

        Assert.Equal([kept.Id], (await GetExistingAsync(id)).Works.Select(work => work.Id));
    }

    [Fact]
    public async Task AddingToADeletedCollectionIsRejected()
    {
        var id = await _catalog.AddCollectionAsync();
        var painting = await _catalog.AddPaintingInCollectionAsync(await _catalog.AddCollectionAsync(), []);
        await _collections.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(
            () => _collections.AddWorksAsync(id, [painting.Id])
        );
    }

    [Fact]
    public async Task DeleteRemovesTheCollection()
    {
        var id = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingInCollectionAsync(id, []);

        await _collections.DeleteAsync(id);

        Assert.Null(await _collections.GetAsync(id));
    }

    [Fact]
    public async Task DeleteKeepsTheCollectionWorks()
    {
        var id = await _catalog.AddCollectionAsync();
        var painting = await _catalog.AddPaintingAsync(
            $"Kept painting {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [id],
            images: []
        );

        await _collections.DeleteAsync(id);

        var keptPainting = await _works.GetBySlugAsync(painting.Slug.Value);
        Assert.NotNull(keptPainting);
        Assert.Empty(keptPainting.Collections);
    }
}
