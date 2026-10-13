using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Database;

// rows, slots and the components placed in them, each test on a page of its own
[Collection(DatabaseCollection.Name)]
public sealed class PageRowRepositoryTests(TestDatabaseFixture database)
{
    private readonly CustomPageRepository _pages = new(database.Site);
    private readonly PageRowRepository _rows = new(database.Site);
    private readonly RowSlotRepository _slots = new(database.Site);
    private readonly CustomComponentRepository _components = new(database.Site);

    private async Task<CustomPageId> AddPageAsync()
    {
        var name = $"Page {Guid.NewGuid():n}";

        return await _pages.AddAsync(name, CustomPageAddress.FromName(name));
    }

    private async Task<IReadOnlyList<PageRow>> RowsAsync(CustomPageId pageId) => (await _pages.GetLayoutAsync(pageId)).Rows;

    private async Task<PageRow> RowAsync(CustomPageId pageId, PageRowId rowId) =>
        Assert.Single(await RowsAsync(pageId), row => row.Id == rowId);

    [Fact]
    public async Task ANewRowIsOneEmptySlotAtTheBottom()
    {
        var pageId = await AddPageAsync();
        var first = await _rows.AddAsync(pageId);
        var second = await _rows.AddAsync(pageId);

        var rows = await RowsAsync(pageId);

        Assert.Equal([first, second], rows.Select(row => row.Id));
        Assert.Equal((RowWidths.Whole, RowAlignment.Start), (rows[1].Widths, rows[1].Alignment));
        Assert.Empty(Assert.Single(rows[1].Slots).Stack);
    }

    [Fact]
    public async Task RowsReorderAndAStaleOrderIsRefused()
    {
        var pageId = await AddPageAsync();
        var first = await _rows.AddAsync(pageId);
        var second = await _rows.AddAsync(pageId);

        await _rows.ReorderAsync(pageId, [second, first]);

        Assert.Equal([second, first], (await RowsAsync(pageId)).Select(row => row.Id));
        await _rows.AddAsync(pageId);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _rows.ReorderAsync(pageId, [first, second]));
    }

    [Fact]
    public async Task ASlotAddedOrRemovedTakesTheFirstSplitForTheNewCount()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);

        await _slots.AddAsync(rowId, slotCount: 1);
        await _slots.AddAsync(rowId, slotCount: 2);
        var three = await RowAsync(pageId, rowId);
        Assert.Equal((RowWidths.Thirds, 3), (three.Widths, three.Slots.Count));

        await _slots.DeleteAsync(rowId, three.Slots[1].Id, slotCount: 3);
        var two = await RowAsync(pageId, rowId);
        Assert.Equal(RowWidths.Halves, two.Widths);
        Assert.Equal([three.Slots[0].Id, three.Slots[2].Id], two.Slots.Select(slot => slot.Id));
    }

    // another tab added a slot, so the split this one chose was for the wrong number of them
    [Fact]
    public async Task AChangeMadeForAnotherSlotCountIsRefused()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        await _slots.AddAsync(rowId, slotCount: 1);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _slots.AddAsync(rowId, slotCount: 1));
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _rows.SetLayoutAsync(rowId, slotCount: 1, RowWidths.Whole, RowAlignment.Center)
        );

        await _rows.SetLayoutAsync(rowId, slotCount: 2, RowWidths.TwoThirdsThenThird, RowAlignment.End);
        var row = await RowAsync(pageId, rowId);
        Assert.Equal((RowWidths.TwoThirdsThenThird, RowAlignment.End), (row.Widths, row.Alignment));
    }

    [Fact]
    public async Task AStackKeepsItsOrderAndAComponentShowsOncePerSlot()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var viewer = await _components.AddToSlotAsync(slotId, new CollectionViewerSettings());
        var posts = await _components.AddToSlotAsync(slotId, new PostListSettings());

        await _slots.ReorderPlacementsAsync(slotId, [posts, viewer]);

        var stack = Assert.Single((await RowAsync(pageId, rowId)).Slots).Stack;
        Assert.Equal([posts, viewer], stack.Select(component => component.Id));
        Assert.IsType<PostListSettings>(stack[0].Settings);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _slots.PlaceAsync(slotId, viewer));
    }

    [Fact]
    public async Task AComponentOutlivesItsPlacementsAndCanBePlacedElsewhere()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var viewer = await _components.AddToSlotAsync(slotId, new CollectionViewerSettings());

        await _slots.RemovePlacementAsync(slotId, viewer);
        Assert.Empty(Assert.Single(await _components.GetAllAsync(), made => made.Component.Id == viewer).PageNames);

        var otherRowId = await _rows.AddAsync(pageId);
        var otherSlotId = Assert.Single((await RowAsync(pageId, otherRowId)).Slots).Id;
        await _slots.PlaceAsync(otherSlotId, viewer);
        await _rows.DeleteAsync(otherRowId);

        Assert.Single(await _components.GetAllAsync(), made => made.Component.Id == viewer);
    }

    [Fact]
    public async Task AComponentNamesThePagesItsOn()
    {
        var pageId = await AddPageAsync();
        var page = Assert.IsType<CustomPage>(await _pages.GetAsync(pageId));
        var rowId = await _rows.AddAsync(pageId);
        await _slots.AddAsync(rowId, slotCount: 1);
        var slots = (await RowAsync(pageId, rowId)).Slots;
        var viewer = await _components.AddToSlotAsync(slots[0].Id, new CollectionViewerSettings());
        await _slots.PlaceAsync(slots[1].Id, viewer);

        Assert.Equal([page.Name], Assert.Single(await _components.GetAllAsync(), made => made.Component.Id == viewer).PageNames);
    }
}
