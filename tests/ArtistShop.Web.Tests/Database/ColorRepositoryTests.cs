using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ColorRepositoryTests(TestDatabaseFixture database)
{
    private readonly ColorRepository _colors = new(database.Site);

    [Fact]
    public async Task ASaveReplacesEveryChoice()
    {
        var backdrop = RgbColor.Parse("#ff0000").WithOpacityPercent(25);

        await _colors.UpdateAsync(new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Page] = RgbColor.White, [ColorRole.Backdrop] = backdrop }));
        await _colors.UpdateAsync(new SiteColors(new Dictionary<ColorRole, RgbColor> { [ColorRole.Backdrop] = backdrop }));
        var saved = await _colors.GetAsync();
        await _colors.UpdateAsync(SiteColors.Default);

        Assert.Equal([KeyValuePair.Create(ColorRole.Backdrop, backdrop)], saved.Chosen);
        Assert.Empty((await _colors.GetAsync()).Chosen);
    }
}
