using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using Dapper;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class FontRepositoryTests(TestDatabaseFixture database)
{
    private readonly FontRepository _fonts = new(database.PlatformDataSource);

    [Fact]
    public async Task ReorderingReplacesTheOrder()
    {
        await _fonts.ReorderAsync([Font.Bitter, Font.Roboto, Font.Cinzel]);
        await _fonts.ReorderAsync([Font.Cinzel, Font.Bitter]);

        Assert.Equal([Font.Cinzel, Font.Bitter], await _fonts.GetOrderAsync());
    }

    // a font since taken out of Font, or a number where a name belongs
    [Fact]
    public async Task ANameThatIsntAFontIsIgnored()
    {
        await using var connection = database.PlatformDataSource.CreateConnection();
        await connection.ExecuteAsync("SELECT reorder_fonts(@Fonts)", new { Fonts = (string[])["Removed", "Bitter", "3"] });

        Assert.Equal([Font.Bitter], await _fonts.GetOrderAsync());
    }
}
