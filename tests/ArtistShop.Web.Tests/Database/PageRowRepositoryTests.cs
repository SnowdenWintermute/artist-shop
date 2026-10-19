using System.Text.Json.Nodes;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
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
    private readonly UploadedImageRepository _library = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    // the fixture's database is shared, so each test's components have names of their own
    private static string UniqueName() => $"Component {Guid.NewGuid():n}";

    private async Task<CustomPageId> AddPageAsync()
    {
        var name = $"Page {Guid.NewGuid():n}";

        return await _pages.AddAsync(name, CustomPageAddress.FromName(name));
    }

    private async Task<IReadOnlyList<PageRow>> RowsAsync(CustomPageId pageId) => (await _pages.GetLayoutAsync(pageId)).Rows;

    private async Task<PageRow> RowAsync(CustomPageId pageId, PageRowId rowId) =>
        Assert.Single(await RowsAsync(pageId), row => row.Id == rowId);

    private Task<string> AddLibraryImageAsync(string altText) => UploadedImageTestData.AddAsync(_library, "pier.jpg", altText);

    // a new row with this background, on a page of its own
    private async Task<(CustomPageId PageId, PageRowId RowId)> AddRowShowingAsync(RowBackground background)
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        await _rows.SetSettingsAsync(rowId, (await RowAsync(pageId, rowId)).Settings with { Background = background });
        return (pageId, rowId);
    }

    [Fact]
    public async Task ANewRowIsOneEmptySlotAtTheBottom()
    {
        var pageId = await AddPageAsync();
        var first = await _rows.AddAsync(pageId);
        var second = await _rows.AddAsync(pageId);

        var rows = await RowsAsync(pageId);

        Assert.Equal([first, second], rows.Select(row => row.Id));
        Assert.Equal((RowWidths.Whole, RowAlignment.Start), (rows[1].Widths, rows[1].Alignment));
        Assert.Equal((RowContentWidth.Page, RowPadding.New, RowGap.Medium), (rows[1].ContentWidth, rows[1].Padding, rows[1].Gap));
        Assert.Empty(Assert.Single(rows[1].Slots).Stack);
    }

    [Fact]
    public async Task RowSettingsAreSavedTogetherAndARemovedRowRefusesThem()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var settings = new RowSettings(
            RowHeight.FillScreen,
            RowContentWidth.EdgeToEdge,
            new RowPadding(RowVerticalPadding.None, RowVerticalPadding.Large, RowSidePadding.Small),
            RowGap.None,
            new ThemeKey.Preset(ThemePreset.Dark),
            new RowBackground.Color(RgbColor.Parse("#123456"))
        );

        await _rows.SetSettingsAsync(rowId, settings);

        Assert.Equal(settings, (await RowAsync(pageId, rowId)).Settings);
        await _rows.DeleteAsync(rowId);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _rows.SetSettingsAsync(rowId, settings));
    }

    [Fact]
    public async Task AnImageBackgroundIsSavedWithItsSourceFitAndColour()
    {
        var storageKey = await AddLibraryImageAsync("Pier");
        var work = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName());
        RowBackground[] backgrounds =
        [
            new RowBackground.Image(new ImageSource.Library(storageKey), ImageFit.Contain, RgbColor.Parse("#123456")),
            new RowBackground.Image(new ImageSource.Work(work.Id, StorageKey: null), ImageFit.Cover, Behind: null),
            new RowBackground.Image(Source: null, ImageFit.Cover, Behind: null),
        ];

        foreach (var background in backgrounds)
        {
            var (pageId, rowId) = await AddRowShowingAsync(background);

            Assert.Equal(background, (await RowAsync(pageId, rowId)).Background);
        }
    }

    // counted where the Images page says it's used, and listed in the confirm dialogs. Deleted, the
    // colour behind the image stays as the row's colour; with none, the row has no background
    [Fact]
    public async Task DeletingABackgroundsImageOrWorkLeavesItsColour()
    {
        var altText = UniqueName();
        var storageKey = await AddLibraryImageAsync(altText);
        var work = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName());
        var colour = RgbColor.Parse("#123456");
        var onImage = await AddRowShowingAsync(new RowBackground.Image(new ImageSource.Library(storageKey), ImageFit.Cover, colour));
        var onWork = await AddRowShowingAsync(new RowBackground.Image(new ImageSource.Work(work.Id, StorageKey: null), ImageFit.Cover, Behind: null));
        var pageNames = (await _pages.GetAllAsync()).ToDictionary(page => page.Id, page => page.Name);

        Assert.Equal(1, Assert.Single((await _library.GetPageAsync(altText, pageNumber: 1)).Items).UsedInCount);
        Assert.Equal([pageNames[onImage.PageId]], await _rows.GetPagesWithBackgroundsShowingImagesAsync([storageKey]));
        Assert.Equal([pageNames[onWork.PageId]], await _rows.GetPagesWithBackgroundsShowingWorksAsync([work.Id]));
        await _library.DeleteAsync([storageKey]);
        await new WorkRepository(database.Site).DeleteAsync(work.Id);

        Assert.Equal(new RowBackground.Color(colour), (await RowAsync(onImage.PageId, onImage.RowId)).Background);
        Assert.Equal(new RowBackground.None(), (await RowAsync(onWork.PageId, onWork.RowId)).Background);
    }

    [Fact]
    public async Task ARowOnASavedThemeGoesBackToTheWebsitesWhenTheThemeIsDeleted()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var themes = new ThemeRepository(database.Site);
        var savedId = await themes.AddAsync($"Theme {Guid.NewGuid():n}", ThemePresets.Paper.Theme);
        var onSaved = (await RowAsync(pageId, rowId)).Settings with { Theme = new ThemeKey.Saved(savedId) };

        await _rows.SetSettingsAsync(rowId, onSaved);
        Assert.Equal(new ThemeKey.Saved(savedId), (await RowAsync(pageId, rowId)).Theme?.Key);

        await themes.DeleteAsync(savedId);
        Assert.Null((await RowAsync(pageId, rowId)).Theme);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _rows.SetSettingsAsync(rowId, onSaved));
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
        var viewer = await _components.AddToSlotAsync(slotId, UniqueName(), CardGridSettings.AllCollections);
        var posts = await _components.AddToSlotAsync(slotId, UniqueName(), new PostListSettings());

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
        var viewer = await _components.AddToSlotAsync(slotId, UniqueName(), CardGridSettings.AllCollections);

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
        var viewer = await _components.AddToSlotAsync(slots[0].Id, UniqueName(), CardGridSettings.AllCollections);
        await _slots.PlaceAsync(slots[1].Id, viewer);

        Assert.Equal([page.Name], Assert.Single(await _components.GetAllAsync(), made => made.Component.Id == viewer).PageNames);
    }

    [Fact]
    public async Task AComponentsNameIsUniqueWhateverItsCase()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var name = UniqueName();
        var first = await _components.AddToSlotAsync(slotId, name, CardGridSettings.AllCollections);
        var second = await _components.AddToSlotAsync(slotId, UniqueName(), new PostListSettings());

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _components.AddToSlotAsync(slotId, name.ToUpperInvariant(), new PostListSettings())
        );
        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _components.UpdateAsync(second, name, new PostListSettings()));
        Assert.Equal(name, Assert.IsType<CustomComponentInUse>(await _components.GetAsync(first)).Component.Name);
    }

    [Fact]
    public async Task AnArticleReadsBackAsItWasSaved()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var id = await _components.AddToSlotAsync(slotId, UniqueName(), new TextSettings(RichText.Empty, TextWidth.Narrow));
        var before = Assert.IsType<CustomComponentInUse>(await _components.GetAsync(id));
        var name = UniqueName();

        var body = """{"ops":[{"insert":"Hello\n"}]}""";

        await _components.UpdateAsync(id, name, new TextSettings(new RichText(body), TextWidth.Full));

        var after = Assert.IsType<CustomComponentInUse>(await _components.GetAsync(id));
        var text = Assert.IsType<TextSettings>(after.Component.Settings);
        Assert.Equal(name, after.Component.Name);
        Assert.Equal(TextWidth.Full, text.Width);
        // jsonb may reorder and respace it, so compared as JSON
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(body), JsonNode.Parse(text.Body.Json)));
        Assert.True(after.UpdatedAt > before.UpdatedAt);
        Assert.Equal([before.PageNames.Single()], after.PageNames);
    }

    [Fact]
    public async Task ADeletedComponentIsTakenOffEveryPageItWasOn()
    {
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        await _slots.AddAsync(rowId, slotCount: 1);
        var slots = (await RowAsync(pageId, rowId)).Slots;
        var otherPageId = await AddPageAsync();
        var otherRowId = await _rows.AddAsync(otherPageId);
        var viewer = await _components.AddToSlotAsync(slots[0].Id, UniqueName(), CardGridSettings.AllCollections);
        var kept = await _components.AddToSlotAsync(slots[0].Id, UniqueName(), CardGridSettings.AllCollections);
        await _slots.PlaceAsync(slots[1].Id, viewer);
        await _slots.PlaceAsync(Assert.Single((await RowAsync(otherPageId, otherRowId)).Slots).Id, viewer);

        await _components.DeleteAsync(viewer);
        // gone already: nothing to do
        await _components.DeleteAsync(viewer);

        Assert.Null(await _components.GetAsync(viewer));
        Assert.Equal([[kept], []], (await RowAsync(pageId, rowId)).Slots.Select(slot => slot.Stack.Select(component => component.Id)));
        Assert.Empty(Assert.Single((await RowAsync(otherPageId, otherRowId)).Slots).Stack);
    }

    // the mention goes with the component; the work stays
    [Fact]
    public async Task DeletingAComponentLeavesTheWorksItMentions()
    {
        var workId = (await new CatalogTestData(database.Site).AddPaintingAsync($"Painting {Guid.NewGuid():n}", [], [], [])).Id;
        var embedding = new RichText(
            """{"ops":[{"insert":{"artshop-work":{"workId":ID}}},{"insert":"\n"}]}""".Replace("ID", $"{workId.Value}")
        );
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var id = await _components.AddToSlotAsync(slotId, UniqueName(), new TextSettings(embedding, TextWidth.Narrow));

        await _components.DeleteAsync(id);

        Assert.Empty(await _pages.GetMentioningWorkAsync(workId));
        Assert.NotNull(await new WorkRepository(database.Site).GetByIdAsync(workId));
    }

    [Fact]
    public async Task SavingADeletedComponentSaysItChanged() =>
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _components.UpdateAsync(new CustomComponentId(int.MaxValue), UniqueName(), new PostListSettings())
        );

    // as a post's: derived from the text on every save, so a removed embed stops counting
    [Fact]
    public async Task APageMentionsTheWorksItsArticlesEmbed()
    {
        var workId = (await new CatalogTestData(database.Site).AddPaintingAsync($"Painting {Guid.NewGuid():n}", [], [], [])).Id;
        var embedding = new RichText(
            """{"ops":[{"insert":{"artshop-work":{"workId":ID}}},{"insert":"\n"}]}""".Replace("ID", $"{workId.Value}")
        );
        var pageId = await AddPageAsync();
        var rowId = await _rows.AddAsync(pageId);
        var slotId = Assert.Single((await RowAsync(pageId, rowId)).Slots).Id;
        var name = UniqueName();
        var id = await _components.AddToSlotAsync(slotId, name, new TextSettings(embedding, TextWidth.Narrow));

        Assert.Equal([pageId], (await _pages.GetMentioningWorkAsync(workId)).Select(page => page.Id));

        await _components.UpdateAsync(id, name, new TextSettings(RichText.Empty, TextWidth.Narrow));

        Assert.Empty(await _pages.GetMentioningWorkAsync(workId));
    }
}
