using ArtistShop.Web.Domain;
using ArtistShop.Web.Search;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Search;

[Collection(DatabaseCollection.Name)]
public sealed class SqlArtworkTitleSearchTests(TestDatabaseFixture database)
{
    private readonly CatalogTestData _catalog = new(database.Site);
    private readonly SqlArtworkTitleSearch _search = new(database.Site);

    [Fact]
    public async Task MatchesPartOfATitleWhateverTheCase()
    {
        var suffix = $"{Guid.NewGuid():n}";
        var painting = await _catalog.AddPaintingAsync($"Harbour at dusk {suffix}", [], [], []);

        var found = await _search.FindAsync($"HARBOUR AT DUSK {suffix}");

        Assert.Equal(painting.Id, Assert.Single(found));
    }

    // the column's own collation is accent-sensitive, which is right for a term name and
    // wrong for a search box
    [Fact]
    public async Task MatchesAnAccentedTitleTypedWithoutTheAccent()
    {
        var suffix = $"{Guid.NewGuid():n}";
        var painting = await _catalog.AddPaintingAsync($"Café {suffix}", [], [], []);

        var found = await _search.FindAsync($"cafe {suffix}");

        Assert.Equal(painting.Id, Assert.Single(found));
    }

    // unescaped, the % would stand for "anything at all" and match the other title too
    [Fact]
    public async Task TreatsAPercentSignInTheTextAsAPercentSign()
    {
        var suffix = $"{Guid.NewGuid():n}";
        var discount = await _catalog.AddPaintingAsync($"Discount 50% {suffix}", [], [], []);
        await _catalog.AddPaintingAsync($"Discount 500 {suffix}", [], [], []);

        var found = await _search.FindAsync($"50% {suffix}");

        Assert.Equal(discount.Id, Assert.Single(found));
    }

    // the procedure takes nvarchar(200) and SQL Server truncates rather than refusing, so a
    // longer text would search for its own first 200 characters
    [Fact]
    public async Task FindsNothingForATextNoTitleCouldHold()
    {
        var tooLong = new string('a', ArtistShopLimits.ArtworkNameMaximumLength + 1);

        Assert.Empty(await _search.FindAsync(tooLong));
    }
}
