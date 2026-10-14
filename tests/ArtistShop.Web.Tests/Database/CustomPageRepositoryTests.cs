using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class CustomPageRepositoryTests(TestDatabaseFixture database)
{
    private readonly CustomPageRepository _pages = new(database.Site);

    [Fact]
    public async Task AWebsiteStartsWithAHomePageHoldingOneCollectionViewer()
    {
        var home = await _pages.GetHomeAsync();
        var layout = await _pages.GetLayoutAsync(home.Id);

        Assert.Equal(new CustomPageAddress("home"), home.Address);
        Assert.True(home.IsHome);
        var row = Assert.Single(layout.Rows);
        Assert.Equal((RowWidths.Whole, RowAlignment.Start), (row.Widths, row.Alignment));
        var component = Assert.Single(Assert.Single(row.Slots).Stack);
        Assert.Equal(CardGridSettings.AllCollections, component.Settings);
    }

    [Fact]
    public async Task APageIsFoundByItsAddressAlone()
    {
        Assert.Equal(await _pages.GetHomeAsync(), await _pages.GetByAddressAsync(new CustomPageAddress("home")));
        Assert.Null(await _pages.GetByAddressAsync(new CustomPageAddress("no-such-page")));
    }

    // the fixture's database is shared, so each test's pages have names and addresses of their own
    private async Task<(CustomPageId Id, string Name, CustomPageAddress Address)> AddUniqueAsync()
    {
        var name = $"Page {Guid.NewGuid():n}";
        var address = CustomPageAddress.FromName(name);

        return (await _pages.AddAsync(name, address), name, address);
    }

    [Fact]
    public async Task AnAddedPageIsEmptyAndNotHome()
    {
        var (id, name, address) = await AddUniqueAsync();

        Assert.Equal(new CustomPage(id, name, address, IsHome: false, IsBlog: false), await _pages.GetAsync(id));
        Assert.Empty((await _pages.GetLayoutAsync(id)).Rows);
    }

    [Fact]
    public async Task ANameOrAddressAnotherPageHasIsRefused()
    {
        var (_, name, address) = await AddUniqueAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _pages.AddAsync(name.ToUpperInvariant(), new CustomPageAddress($"{address.Value}-2"))
        );
        await Assert.ThrowsAsync<AddressAlreadyInUseException>(() => _pages.AddAsync($"{name} 2", address));
    }

    [Fact]
    public async Task ChangingADeletedPageSaysItChanged()
    {
        var (id, name, address) = await AddUniqueAsync();
        await _pages.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _pages.UpdateAsync(id, name, address));
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _pages.MakeHomeAsync(id));
    }

    [Fact]
    public async Task TheHomeAndBlogPagesCantBeDeleted()
    {
        var home = await _pages.GetHomeAsync();
        var blog = await _pages.GetBlogAsync();

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _pages.DeleteAsync(home.Id));
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _pages.DeleteAsync(blog.Id));
        Assert.Equal(home, await _pages.GetAsync(home.Id));
        Assert.Equal(blog, await _pages.GetAsync(blog.Id));
    }

    [Fact]
    public async Task AWebsiteStartsWithABlogPageHoldingAPostList()
    {
        var blog = await _pages.GetBlogAsync();

        Assert.Equal(new CustomPageAddress("posts"), blog.Address);
        Assert.Contains(blog.Id, await _pages.GetIdsHoldingAsync(CustomComponentType.PostList));
        Assert.DoesNotContain((await _pages.GetHomeAsync()).Id, await _pages.GetIdsHoldingAsync(CustomComponentType.PostList));
    }

    [Fact]
    public async Task ANavWithADeletedPageIsRefusedAndLeavesTheNavAsItWas()
    {
        var nav = new NavEntryRepository(database.Site);
        var before = await nav.GetAllAsync();
        var (id, _, _) = await AddUniqueAsync();
        await _pages.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => nav.SetAsync([.. before.Select(page => page.Id), id]));
        Assert.Equal(before, await nav.GetAllAsync());
    }
}
