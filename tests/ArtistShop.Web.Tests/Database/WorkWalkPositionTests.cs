using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

// Other tests' collections share the database, so these compare positions within the collections each test
// adds rather than reading them whole. New collections go last, which the one test of the total leans on
[Collection(DatabaseCollection.Name)]
public sealed class WorkWalkPositionTests(TestDatabaseFixture database)
{
    private readonly WorkRepository _works = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private Task<WorkIdentifiers> AddAsync(CollectionId collectionId, int imageCount) =>
        _catalog.AddPaintingInCollectionAsync(
            collectionId,
            [.. Enumerable.Range(0, imageCount).Select(_ => CatalogTestData.CreateTestImage())]
        );

    private async Task<int> EarlierImageCountAsync(CollectionId collectionId, WorkIdentifiers work) =>
        (await _works.GetWalkPositionAsync(collectionId, work.Id)).EarlierImageCount;

    // each work's pictures follow on from the one before's, in the place the artist dragged
    // them into rather than the order they were added in
    [Fact]
    public async Task CountsTheImagesBeforeInTheOrderTheArtistDragged()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await AddAsync(collectionId, imageCount: 1);
        var middle = await AddAsync(collectionId, imageCount: 2);
        var last = await AddAsync(collectionId, imageCount: 3);

        await _collections.ReorderWorksAsync(collectionId, [last.Id, middle.Id, first.Id]);

        var lastsPlace = await EarlierImageCountAsync(collectionId, last);

        Assert.Equal(lastsPlace + 3, await EarlierImageCountAsync(collectionId, middle));
        Assert.Equal(lastsPlace + 3 + 2, await EarlierImageCountAsync(collectionId, first));
    }

    // the walk runs on from one collection into the next, and work with no photograph adds nothing,
    // as Previous and Next step over it
    [Fact]
    public async Task RunsOnAcrossCollectionPastUnphotographedWork()
    {
        var firstCollection = await _catalog.AddCollectionAsync();
        var inFirst = await AddAsync(firstCollection, imageCount: 2);

        var between = await _catalog.AddCollectionAsync();
        await AddAsync(between, imageCount: 0);

        var lastCollection = await _catalog.AddCollectionAsync();
        var inLast = await AddAsync(lastCollection, imageCount: 1);

        Assert.Equal(
            await EarlierImageCountAsync(firstCollection, inFirst) + 2,
            await EarlierImageCountAsync(lastCollection, inLast)
        );
    }

    // counted once for each place it has in the walk, since the walk visits it once in each
    [Fact]
    public async Task CountsAWorkInTwoCollectionsInBoth()
    {
        var firstCollection = await _catalog.AddCollectionAsync();
        var lastCollection = await _catalog.AddCollectionAsync();
        var inBoth = await _catalog.AddPaintingAsync(
            $"Collection test painting {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [firstCollection, lastCollection],
            images: [CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage()]
        );
        var after = await AddAsync(lastCollection, imageCount: 1);

        var firstPlace = await EarlierImageCountAsync(firstCollection, inBoth);

        Assert.Equal(firstPlace + 2, await EarlierImageCountAsync(lastCollection, inBoth));
        Assert.Equal(firstPlace + 2 + 2, await EarlierImageCountAsync(lastCollection, after));
    }

    // the last work of the last collection has nothing after it but its own pictures
    [Fact]
    public async Task TotalEndsWithTheLastWorksPictures()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var last = await AddAsync(collectionId, imageCount: 3);

        var position = await _works.GetWalkPositionAsync(collectionId, last.Id);

        Assert.Equal(position.EarlierImageCount + 3, position.TotalImageCount);
    }
}
