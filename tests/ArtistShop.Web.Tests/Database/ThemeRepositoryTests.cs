using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ThemeRepositoryTests(TestDatabaseFixture database)
{
    private readonly ThemeRepository _themes = new(database.Site);

    // the fixture's database is shared, so each test's themes have names of their own
    private static string UniqueName() => $"Theme {Guid.NewGuid():n}";

    private static Theme Choosing(params (ColorRole Role, RgbColor Color)[] extra) =>
        new(
            new ThemeColors(
                new Dictionary<ColorRole, RgbColor>(
                    [
                        KeyValuePair.Create(ColorRole.Page, RgbColor.Parse("#202020")),
                        KeyValuePair.Create(ColorRole.Ink, RgbColor.White),
                        KeyValuePair.Create(ColorRole.Accent, RgbColor.Parse("#99c1f1")),
                        .. extra.Select(choice => KeyValuePair.Create(choice.Role, choice.Color)),
                    ]
                )
            )
        );

    [Fact]
    public async Task AThemeReadsBackAsItWasSaved()
    {
        var name = UniqueName();
        var backdrop = RgbColor.Parse("#ff0000").WithOpacityPercent(25);

        var id = await _themes.AddAsync(name, Choosing((ColorRole.Backdrop, backdrop)));
        var saved = Assert.Single(await _themes.GetAllAsync(), theme => theme.Id == id);

        Assert.Equal(name, saved.Name);
        Assert.Equal(Choosing((ColorRole.Backdrop, backdrop)).Colors.Chosen, saved.Theme.Colors.Chosen);
    }

    [Fact]
    public async Task ANameAnotherThemeHasIsRefusedWhateverItsCase()
    {
        var name = UniqueName();
        await _themes.AddAsync(name, Choosing());

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _themes.AddAsync(name.ToUpperInvariant(), Choosing()));
    }

    [Fact]
    public async Task SavingADeletedThemeSaysItChanged()
    {
        var id = await _themes.AddAsync(UniqueName(), Choosing());
        await _themes.DeleteAsync(id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _themes.UpdateAsync(id, UniqueName(), Choosing()));
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _themes.UseAsync(new ThemeKey.Saved(id)));
    }

    [Fact]
    public async Task DeletingTheThemeInUsePutsTheWebsiteBackOnPaper()
    {
        var id = await _themes.AddAsync(UniqueName(), Choosing());
        await _themes.UseAsync(new ThemeKey.Saved(id));
        var inUse = await _themes.GetInUseKeyAsync();

        await _themes.DeleteAsync(id);

        Assert.Equal(new ThemeKey.Saved(id), inUse);
        Assert.Equal(new ThemeKey.Preset(ThemePreset.Paper), await _themes.GetInUseKeyAsync());
        Assert.Equal(RgbColor.Parse("#f5f5f5"), (await _themes.GetInUseAsync()).Colors.Resolve(ColorRole.Page));
    }

    [Fact]
    public async Task APresetCanBeInUse()
    {
        await _themes.UseAsync(new ThemeKey.Preset(ThemePreset.Dark));

        Assert.Equal(new ThemeKey.Preset(ThemePreset.Dark), await _themes.GetInUseKeyAsync());
        Assert.Equal(
            ThemePresets.For(ThemePreset.Dark).Theme.Colors.Chosen,
            (await _themes.GetInUseAsync()).Colors.Chosen
        );

        await _themes.UseAsync(new ThemeKey.Preset(ThemePreset.Paper));
    }
}
