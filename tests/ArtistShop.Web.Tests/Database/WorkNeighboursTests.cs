using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

// Other tests' collections share the database, and new collections go last, so these only look within or
// between the collections each test adds, and past the last of them
[Collection(DatabaseCollection.Name)]
public sealed class WorkNeighboursTests(TestDatabaseFixture database)
{
    private readonly WorkRepository _works = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<WorkIdentifiers> AddAsync(CollectionId collectionId, int imageCount) =>
        _catalog.AddPaintingInCollectionAsync(
            collectionId,
            [.. Enumerable.Range(0, imageCount).Select(_ => CatalogTestData.CreateTestImage())]
        );

    private async Task<CollectionSlug> SlugOfAsync(CollectionId collectionId) =>
        (await _collections.GetAsync(collectionId))?.Slug
        ?? throw new InvalidOperationException($"Collection {collectionId.Value} wasn't added.");

    // the place the artist dragged each work into, not the order they were added in
    [Fact]
    public async Task FollowsTheOrderTheArtistDragged()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await AddAsync(collectionId, imageCount: 1);
        var middle = await AddAsync(collectionId, imageCount: 1);
        var last = await AddAsync(collectionId, imageCount: 1);

        await _collections.ReorderWorksAsync(collectionId, [last.Id, middle.Id, first.Id]);

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            middle.Id,
            onlyWorksWithImages: true
        );

        Assert.Equal(last.Slug, neighbours.Previous?.Slug);
        Assert.Equal(first.Slug, neighbours.Next?.Slug);
    }

    // so stepping back past the first image can land on the previous work's last
    [Fact]
    public async Task CountsEachNeighboursImages()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        await AddAsync(collectionId, imageCount: 3);
        var middle = await AddAsync(collectionId, imageCount: 1);
        await AddAsync(collectionId, imageCount: 1);

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            middle.Id,
            onlyWorksWithImages: true
        );

        Assert.Equal(3, neighbours.Previous?.ImageCount);
        Assert.Equal(1, neighbours.Next?.ImageCount);
    }

    // past either end of a collection comes the collection beside it, and the collection between has only
    // unphotographed work, so a visitor steps over it
    [Fact]
    public async Task RunsOnIntoTheNearestCollectionWithSomethingToSee()
    {
        var first = await _catalog.AddCollectionAsync();
        await AddAsync(first, imageCount: 1);
        var firstsLast = await AddAsync(first, imageCount: 3);

        var between = await _catalog.AddCollectionAsync();
        await AddAsync(between, imageCount: 0);

        var last = await _catalog.AddCollectionAsync();
        var lastsFirst = await AddAsync(last, imageCount: 2);
        await AddAsync(last, imageCount: 1);

        var fromFirstsLast = await _works.GetNeighboursAsync(
            first,
            firstsLast.Id,
            onlyWorksWithImages: true
        );
        var fromLastsFirst = await _works.GetNeighboursAsync(
            last,
            lastsFirst.Id,
            onlyWorksWithImages: true
        );

        Assert.Equal(new WorkInCollection(lastsFirst.Slug, await SlugOfAsync(last), 2), fromFirstsLast.Next);
        Assert.Equal(new WorkInCollection(firstsLast.Slug, await SlugOfAsync(first), 3), fromLastsFirst.Previous);
    }

    [Fact]
    public async Task HasNothingPastTheLastCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await AddAsync(collectionId, imageCount: 1);
        var last = await AddAsync(collectionId, imageCount: 1);

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            last.Id,
            onlyWorksWithImages: true
        );

        Assert.Equal(first.Slug, neighbours.Previous?.Slug);
        Assert.Null(neighbours.Next);
    }

    // a visitor is only sent on to work there is something to look at
    [Fact]
    public async Task StepsOverWorkWithNoPhotograph()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await AddAsync(collectionId, imageCount: 1);
        await AddAsync(collectionId, imageCount: 0);
        var last = await AddAsync(collectionId, imageCount: 1);

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            first.Id,
            onlyWorksWithImages: true
        );

        Assert.Equal(last.Slug, neighbours.Next?.Slug);
    }

    // the artist sees everything, so the rule is a parameter rather than the procedure's own
    [Fact]
    public async Task KeepsUnphotographedWorkForTheArtist()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await AddAsync(collectionId, imageCount: 1);
        var unphotographed = await AddAsync(collectionId, imageCount: 0);

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            first.Id,
            onlyWorksWithImages: false
        );

        Assert.Equal(unphotographed.Slug, neighbours.Next?.Slug);
    }

    // a work that isn't in the collection anchors nothing, so neither side is offered
    [Fact]
    public async Task HasNoNeighboursOutsideTheCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        await AddAsync(collectionId, imageCount: 1);
        var elsewhere = await _catalog.AddPaintingAsync(
            $"Elsewhere {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: [CatalogTestData.CreateTestImage()]
        );

        var neighbours = await _works.GetNeighboursAsync(
            collectionId,
            elsewhere.Id,
            onlyWorksWithImages: true
        );

        Assert.Null(neighbours.Previous);
        Assert.Null(neighbours.Next);
    }
}
