using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

// Every test here shares the database with the others, so each one puts its works in a
// collection of its own and filters by it: without that, another class's rows would be on the page
[Collection(DatabaseCollection.Name)]
public sealed class WorkListTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly ProductTypeRepository _productTypes = new(database.Site);
    private readonly CollectionRepository _collections = new(database.Site);

    private static WorkListFilter FilterFor(
        CollectionId collectionId,
        IReadOnlyList<VocabularyTermId>? termIds = null,
        bool? hasImages = null,
        bool? isForSale = null,
        WorkListSort sort = WorkListSort.TitleAscending,
        int pageNumber = 1
    ) =>
        new(
            WorkTypeIds: [],
            VocabularyTermIds: termIds ?? [],
            Collection: new WorkCollectionFilter.InCollection(collectionId),
            SearchText: null,
            HasImages: hasImages,
            IsForSale: isForSale,
            Sort: sort,
            PageNumber: pageNumber
        );

    private Task<WorkListPage> ListAsync(WorkListFilter filter) =>
        _works.GetListAsync(filter, searchMatches: null, excludedCollectionId: null);

    [Fact]
    public async Task SeveralTermsFromOneVocabularyWidenTheSearch()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var oil = await _catalog.AddTermAsync(vocabularyId);
        var pastel = await _catalog.AddTermAsync(vocabularyId);

        var oilPainting = await _catalog.AddPaintingAsync(
            $"Oil {Guid.NewGuid():n}",
            termIds: [oil],
            collectionIds: [collectionId],
            images: []
        );
        var pastelPainting = await _catalog.AddPaintingAsync(
            $"Pastel {Guid.NewGuid():n}",
            termIds: [pastel],
            collectionIds: [collectionId],
            images: []
        );
        await _catalog.AddPaintingAsync(
            $"Neither {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        var page = await ListAsync(FilterFor(collectionId, termIds: [oil, pastel]));

        Assert.Equal(
            [oilPainting.Id, pastelPainting.Id],
            [.. page.Items.Select(item => item.Id).Order(WorkIdOrder)]
        );
    }

    [Fact]
    public async Task ATermFromASecondVocabularyNarrowsIt()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var oil = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        var paper = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());

        await _catalog.AddPaintingAsync(
            $"Oil only {Guid.NewGuid():n}",
            termIds: [oil],
            collectionIds: [collectionId],
            images: []
        );
        var both = await _catalog.AddPaintingAsync(
            $"Oil on paper {Guid.NewGuid():n}",
            termIds: [oil, paper],
            collectionIds: [collectionId],
            images: []
        );

        var page = await ListAsync(FilterFor(collectionId, termIds: [oil, paper]));

        Assert.Equal(both.Id, Assert.Single(page.Items).Id);
    }

    // the order the artist dragged the works into, which is what the public collection page
    // shows. It is the only sort that reads a column outside works
    [Fact]
    public async Task SortsByThePlaceTheArtistGaveEachWorkInTheCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var first = await _catalog.AddPaintingAsync(
            $"First {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );
        var second = await _catalog.AddPaintingAsync(
            $"Second {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        await _collections.ReorderWorksAsync(collectionId, [second.Id, first.Id]);
        var page = await ListAsync(FilterFor(collectionId, sort: WorkListSort.CollectionOrder));

        Assert.Equal([second.Id, first.Id], page.Items.Select(item => item.Id));
    }

    // the page that adds works to a collection lists only those not in it yet
    [Fact]
    public async Task LeavesOutTheWorksInTheExcludedCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var excludedCollectionId = await _catalog.AddCollectionAsync();
        var outside = await _catalog.AddPaintingInCollectionAsync(collectionId, []);
        await _catalog.AddPaintingAsync(
            $"Already added {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId, excludedCollectionId],
            images: []
        );

        var page = await _works.GetListAsync(FilterFor(collectionId), searchMatches: null, excludedCollectionId);

        Assert.Equal(outside.Id, Assert.Single(page.Items).Id);
    }

    // no collection of its own to filter by, so the search matches hold the list to this test's works
    [Fact]
    public async Task ListsOnlyTheWorksInNoCollection()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var inCollection = await _catalog.AddPaintingInCollectionAsync(collectionId, []);
        var inNoCollection = await _catalog.AddPaintingAsync(
            $"Unsorted {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            images: []
        );

        var page = await _works.GetListAsync(
            FilterFor(collectionId) with { Collection = new WorkCollectionFilter.InNoCollection() },
            searchMatches: [inCollection.Id, inNoCollection.Id],
            excludedCollectionId: null
        );

        Assert.Equal(inNoCollection.Id, Assert.Single(page.Items).Id);
    }

    // the public pages link by slug, so the list has to carry it
    [Fact]
    public async Task ReadsBackTheSlug()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var added = await _catalog.AddPaintingAsync(
            $"Harbour {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        var page = await ListAsync(FilterFor(collectionId));

        Assert.Equal(added.Slug, Assert.Single(page.Items).Slug);
    }

    [Fact]
    public async Task FiltersByWhetherThereAreImages()
    {
        var collectionId = await _catalog.AddCollectionAsync();

        var photographed = await _catalog.AddPaintingAsync(
            $"Photographed {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: [CatalogTestData.CreateTestImage()]
        );
        var imageless = await _catalog.AddPaintingAsync(
            $"Imageless {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        var withImages = await ListAsync(FilterFor(collectionId, hasImages: true));
        var withoutImages = await ListAsync(FilterFor(collectionId, hasImages: false));
        var either = await ListAsync(FilterFor(collectionId));

        Assert.Equal(photographed.Id, Assert.Single(withImages.Items).Id);
        Assert.Equal(imageless.Id, Assert.Single(withoutImages.Items).Id);
        Assert.Equal(2, either.Items.Count);
    }

    // a sold out product still belongs to the work, so only stock decides
    [Fact]
    public async Task CountsAnWorkAsForSaleOnlyWhileStockIsLeft()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var original = (await _productTypes.GetAllAsync()).Single(type =>
            type.Name.Value == "Original"
        );

        var available = await _catalog.AddPaintingAsync(
            $"Available {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: [],
            products: [new ProductAddition(original.Id, Label: null, 120m, EditionSize: 1, Stock: 1)],
            duration: null
        );
        var soldOut = await _catalog.AddPaintingAsync(
            $"Sold {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: [],
            products:
            [
                new ProductAddition(original.Id, Label: null, Price: null, EditionSize: 1, Stock: 0),
            ],
            duration: null
        );

        var forSale = await ListAsync(FilterFor(collectionId, isForSale: true));
        var notForSale = await ListAsync(FilterFor(collectionId, isForSale: false));

        Assert.Equal(available.Id, Assert.Single(forSale.Items).Id);
        Assert.True(Assert.Single(forSale.Items).IsForSale);
        Assert.Equal(soldOut.Id, Assert.Single(notForSale.Items).Id);
    }

    [Fact]
    public async Task SortsByTitleInBothDirections()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var suffix = $"{Guid.NewGuid():n}";

        await _catalog.AddPaintingAsync($"B {suffix}", [], [collectionId], []);
        await _catalog.AddPaintingAsync($"A {suffix}", [], [collectionId], []);
        await _catalog.AddPaintingAsync($"C {suffix}", [], [collectionId], []);

        var ascending = await ListAsync(FilterFor(collectionId, sort: WorkListSort.TitleAscending));
        var descending = await ListAsync(FilterFor(collectionId, sort: WorkListSort.TitleDescending));

        Assert.Equal(
            [$"A {suffix}", $"B {suffix}", $"C {suffix}"],
            [.. ascending.Items.Select(item => item.Name.Value)]
        );
        Assert.Equal(
            [$"C {suffix}", $"B {suffix}", $"A {suffix}"],
            [.. descending.Items.Select(item => item.Name.Value)]
        );
    }

    // an undated work would otherwise lead the oldest-first page, since NULL sorts first
    [Fact]
    public async Task AnUndatedWorkSinksToTheBottomOfEitherDateSort()
    {
        var collectionId = await _catalog.AddCollectionAsync();

        var older = await AddDatedPaintingAsync(collectionId, new PartialDate(new DateOnly(2001, 1, 1), DatePrecision.Year));
        var newer = await AddDatedPaintingAsync(collectionId, new PartialDate(new DateOnly(2019, 1, 1), DatePrecision.Year));
        var undated = await _catalog.AddPaintingAsync(
            $"Undated {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        var oldestFirst = await ListAsync(FilterFor(collectionId, sort: WorkListSort.DateCreatedOldest));
        var newestFirst = await ListAsync(FilterFor(collectionId, sort: WorkListSort.DateCreatedNewest));

        Assert.Equal([older.Id, newer.Id, undated.Id], [.. oldestFirst.Items.Select(item => item.Id)]);
        Assert.Equal([newer.Id, older.Id, undated.Id], [.. newestFirst.Items.Select(item => item.Id)]);
    }

    // the titles are identical on purpose: with nothing but the title to sort by, only the
    // id tie-break stops a row landing on both pages or on neither
    [Fact]
    public async Task NoWorkAppearsOnTwoPagesWhenTheTitlesTie()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var pageSize = ArtistShopLimits.WorkListPageSize;
        var count = pageSize + 3;
        var name = $"Tied {Guid.NewGuid():n}";
        var typeId = await _catalog.GetPaintingTypeIdAsync();

        await _works.AddManyAsync(
            [
                .. Enumerable
                    .Range(0, count)
                    .Select(_ =>
                        CatalogTestData.CreateWorkAddition(
                            typeId,
                            name,
                            termIds: [],
                            collectionIds: [collectionId],
                            images: [],
                            products: [],
                            duration: null
                        )
                    ),
            ]
        );

        var firstPage = await ListAsync(FilterFor(collectionId));
        var secondPage = await ListAsync(FilterFor(collectionId, pageNumber: 2));

        Assert.Equal(pageSize, firstPage.Items.Count);
        Assert.Equal(count - pageSize, secondPage.Items.Count);
        // the count is of everything that matched, not of the page
        Assert.Equal(count, firstPage.TotalCount);
        Assert.Equal(count, secondPage.TotalCount);
        Assert.Equal(2, firstPage.PageCount);

        var ids = firstPage.Items.Concat(secondPage.Items).Select(item => item.Id);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact]
    public async Task ReadsBackTheCollectionNamesTheImageCountAndThePrimaryImage()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        var otherCollectionId = await _catalog.AddCollectionAsync();
        var images = new[] { CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage() };

        await _catalog.AddPaintingAsync(
            $"Two collections {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId, otherCollectionId],
            images: images
        );

        var item = Assert.Single((await ListAsync(FilterFor(collectionId))).Items);

        Assert.Equal(2, item.ImageCount);
        Assert.Equal(images[0].StorageKey, item.PrimaryImage?.StorageKey);
        Assert.NotNull(item.CollectionNames);
        Assert.Contains(", ", item.CollectionNames);
    }

    // an empty list of matches is a search that found nothing, and must not read as "no search"
    [Fact]
    public async Task ASearchThatMatchedNothingListsNothing()
    {
        var collectionId = await _catalog.AddCollectionAsync();
        await _catalog.AddPaintingAsync(
            $"Findable {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images: []
        );

        var page = await _works.GetListAsync(FilterFor(collectionId), searchMatches: [], excludedCollectionId: null);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    private static Comparer<WorkId> WorkIdOrder { get; } =
        Comparer<WorkId>.Create((left, right) => left.Value.CompareTo(right.Value));

    private async Task<WorkIdentifiers> AddDatedPaintingAsync(
        CollectionId collectionId,
        PartialDate dateCreated
    ) =>
        await _works.AddAsync(
            CatalogTestData.CreateWorkAddition(
                await _catalog.GetPaintingTypeIdAsync(),
                $"Dated {Guid.NewGuid():n}",
                termIds: [],
                collectionIds: [collectionId],
                images: [],
                products: [],
                duration: null
            ) with
            {
                DateCreated = dateCreated,
            }
        );
}
