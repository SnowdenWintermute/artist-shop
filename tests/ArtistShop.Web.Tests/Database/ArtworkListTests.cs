using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

// Every test here shares the database with the others, so each one puts its artworks in a
// series of its own and filters by it: without that, another class's rows would be on the page
[Collection(DatabaseCollection.Name)]
public sealed class ArtworkListTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.ConnectionFactory);
    private readonly ArtworkRepository _artworks = new(database.ConnectionFactory);
    private readonly ProductTypeRepository _productTypes = new(database.ConnectionFactory);

    private static ArtworkListFilter FilterFor(
        SeriesId seriesId,
        IReadOnlyList<VocabularyTermId>? termIds = null,
        bool? hasImages = null,
        bool? isForSale = null,
        ArtworkListSort sort = ArtworkListSort.TitleAscending,
        int pageNumber = 1
    ) =>
        new(
            ArtworkTypeIds: [],
            VocabularyTermIds: termIds ?? [],
            SeriesId: seriesId,
            SearchText: null,
            HasImages: hasImages,
            IsForSale: isForSale,
            Sort: sort,
            PageNumber: pageNumber
        );

    private Task<ArtworkListPage> ListAsync(ArtworkListFilter filter) =>
        _artworks.GetListAsync(filter, searchMatches: null);

    [Fact]
    public async Task SeveralTermsFromOneVocabularyWidenTheSearch()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var vocabularyId = await _catalog.AddPaintingVocabularyAsync();
        var oil = await _catalog.AddTermAsync(vocabularyId);
        var pastel = await _catalog.AddTermAsync(vocabularyId);

        var oilPainting = await _catalog.AddPaintingAsync(
            $"Oil {Guid.NewGuid():n}",
            termIds: [oil],
            seriesIds: [seriesId],
            images: []
        );
        var pastelPainting = await _catalog.AddPaintingAsync(
            $"Pastel {Guid.NewGuid():n}",
            termIds: [pastel],
            seriesIds: [seriesId],
            images: []
        );
        await _catalog.AddPaintingAsync(
            $"Neither {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: []
        );

        var page = await ListAsync(FilterFor(seriesId, termIds: [oil, pastel]));

        Assert.Equal(
            [oilPainting.Id, pastelPainting.Id],
            [.. page.Items.Select(item => item.Id).Order(ArtworkIdOrder)]
        );
    }

    [Fact]
    public async Task ATermFromASecondVocabularyNarrowsIt()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var oil = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());
        var paper = await _catalog.AddTermAsync(await _catalog.AddPaintingVocabularyAsync());

        await _catalog.AddPaintingAsync(
            $"Oil only {Guid.NewGuid():n}",
            termIds: [oil],
            seriesIds: [seriesId],
            images: []
        );
        var both = await _catalog.AddPaintingAsync(
            $"Oil on paper {Guid.NewGuid():n}",
            termIds: [oil, paper],
            seriesIds: [seriesId],
            images: []
        );

        var page = await ListAsync(FilterFor(seriesId, termIds: [oil, paper]));

        Assert.Equal(both.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task FiltersByWhetherThereAreImages()
    {
        var seriesId = await _catalog.AddSeriesAsync();

        var photographed = await _catalog.AddPaintingAsync(
            $"Photographed {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: [CatalogTestData.CreateTestImage()]
        );
        var imageless = await _catalog.AddPaintingAsync(
            $"Imageless {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: []
        );

        var withImages = await ListAsync(FilterFor(seriesId, hasImages: true));
        var withoutImages = await ListAsync(FilterFor(seriesId, hasImages: false));
        var either = await ListAsync(FilterFor(seriesId));

        Assert.Equal(photographed.Id, Assert.Single(withImages.Items).Id);
        Assert.Equal(imageless.Id, Assert.Single(withoutImages.Items).Id);
        Assert.Equal(2, either.Items.Count);
    }

    // a sold out product still belongs to the artwork, so only stock decides
    [Fact]
    public async Task CountsAnArtworkAsForSaleOnlyWhileStockIsLeft()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var original = (await _productTypes.GetAllAsync()).Single(type =>
            type.Name.Value == "Original"
        );

        var available = await _catalog.AddPaintingAsync(
            $"Available {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: [],
            products: [new ProductAddition(original.Id, Label: null, 120m, EditionSize: 1, Stock: 1)],
            duration: null
        );
        var soldOut = await _catalog.AddPaintingAsync(
            $"Sold {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: [],
            products:
            [
                new ProductAddition(original.Id, Label: null, Price: null, EditionSize: 1, Stock: 0),
            ],
            duration: null
        );

        var forSale = await ListAsync(FilterFor(seriesId, isForSale: true));
        var notForSale = await ListAsync(FilterFor(seriesId, isForSale: false));

        Assert.Equal(available.Id, Assert.Single(forSale.Items).Id);
        Assert.True(Assert.Single(forSale.Items).IsForSale);
        Assert.Equal(soldOut.Id, Assert.Single(notForSale.Items).Id);
    }

    [Fact]
    public async Task SortsByTitleInBothDirections()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var suffix = $"{Guid.NewGuid():n}";

        await _catalog.AddPaintingAsync($"B {suffix}", [], [seriesId], []);
        await _catalog.AddPaintingAsync($"A {suffix}", [], [seriesId], []);
        await _catalog.AddPaintingAsync($"C {suffix}", [], [seriesId], []);

        var ascending = await ListAsync(FilterFor(seriesId, sort: ArtworkListSort.TitleAscending));
        var descending = await ListAsync(FilterFor(seriesId, sort: ArtworkListSort.TitleDescending));

        Assert.Equal(
            [$"A {suffix}", $"B {suffix}", $"C {suffix}"],
            [.. ascending.Items.Select(item => item.Name.Value)]
        );
        Assert.Equal(
            [$"C {suffix}", $"B {suffix}", $"A {suffix}"],
            [.. descending.Items.Select(item => item.Name.Value)]
        );
    }

    // an undated artwork would otherwise lead the oldest-first page, since NULL sorts first
    [Fact]
    public async Task AnUndatedArtworkSinksToTheBottomOfEitherDateSort()
    {
        var seriesId = await _catalog.AddSeriesAsync();

        var older = await AddDatedPaintingAsync(seriesId, new PartialDate(new DateOnly(2001, 1, 1), DatePrecision.Year));
        var newer = await AddDatedPaintingAsync(seriesId, new PartialDate(new DateOnly(2019, 1, 1), DatePrecision.Year));
        var undated = await _catalog.AddPaintingAsync(
            $"Undated {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: []
        );

        var oldestFirst = await ListAsync(FilterFor(seriesId, sort: ArtworkListSort.DateCreatedOldest));
        var newestFirst = await ListAsync(FilterFor(seriesId, sort: ArtworkListSort.DateCreatedNewest));

        Assert.Equal([older.Id, newer.Id, undated.Id], [.. oldestFirst.Items.Select(item => item.Id)]);
        Assert.Equal([newer.Id, older.Id, undated.Id], [.. newestFirst.Items.Select(item => item.Id)]);
    }

    // the titles are identical on purpose: with nothing but the title to sort by, only the
    // id tie-break stops a row landing on both pages or on neither
    [Fact]
    public async Task NoArtworkAppearsOnTwoPagesWhenTheTitlesTie()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var pageSize = CatalogLimits.ArtworkListPageSize;
        var count = pageSize + 3;
        var name = $"Tied {Guid.NewGuid():n}";
        var typeId = await _catalog.GetPaintingTypeIdAsync();

        await _artworks.AddManyAsync(
            [
                .. Enumerable
                    .Range(0, count)
                    .Select(_ =>
                        CatalogTestData.CreateArtworkAddition(
                            typeId,
                            name,
                            termIds: [],
                            seriesIds: [seriesId],
                            images: [],
                            products: [],
                            duration: null
                        )
                    ),
            ]
        );

        var firstPage = await ListAsync(FilterFor(seriesId));
        var secondPage = await ListAsync(FilterFor(seriesId, pageNumber: 2));

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
    public async Task ReadsBackTheSeriesNamesTheImageCountAndThePrimaryImage()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        var otherSeriesId = await _catalog.AddSeriesAsync();
        var images = new[] { CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage() };

        await _catalog.AddPaintingAsync(
            $"Two series {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId, otherSeriesId],
            images: images
        );

        var item = Assert.Single((await ListAsync(FilterFor(seriesId))).Items);

        Assert.Equal(2, item.ImageCount);
        Assert.Equal(images[0].StorageKey, item.PrimaryImage?.StorageKey);
        Assert.NotNull(item.SeriesNames);
        Assert.Contains(", ", item.SeriesNames);
    }

    // an empty list of matches is a search that found nothing, and must not read as "no search"
    [Fact]
    public async Task ASearchThatMatchedNothingListsNothing()
    {
        var seriesId = await _catalog.AddSeriesAsync();
        await _catalog.AddPaintingAsync(
            $"Findable {Guid.NewGuid():n}",
            termIds: [],
            seriesIds: [seriesId],
            images: []
        );

        var page = await _artworks.GetListAsync(FilterFor(seriesId), searchMatches: []);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    private static Comparer<ArtworkId> ArtworkIdOrder { get; } =
        Comparer<ArtworkId>.Create((left, right) => left.Value.CompareTo(right.Value));

    private async Task<ArtworkIdentifiers> AddDatedPaintingAsync(
        SeriesId seriesId,
        PartialDate dateCreated
    ) =>
        await _artworks.AddAsync(
            CatalogTestData.CreateArtworkAddition(
                await _catalog.GetPaintingTypeIdAsync(),
                $"Dated {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [seriesId],
                images: [],
                products: [],
                duration: null
            ) with
            {
                DateCreated = dateCreated,
            }
        );
}
