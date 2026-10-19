using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// What a website looks like and calls things: what the look and wording download writes, and what
// its import compares with. ThemeInUse is Paper when the one in use is a preset ThemePresets no
// longer has, as the public pages then show Paper
public record SiteLook(
    SiteWording Wording,
    NamedTheme ThemeInUse,
    IReadOnlyList<SavedTheme> Themes,
    IReadOnlyList<BrandImageOriginal> BrandImages
)
{
    public static async Task<SiteLook> LoadAsync(
        WordingRepository wordingRepository,
        ThemeRepository themeRepository,
        BrandImageStore brandImageStore
    ) =>
        new(
            await wordingRepository.GetAsync(),
            await themeRepository.GetShownAsync(),
            await themeRepository.GetAllAsync(),
            await brandImageStore.GetAllOriginalsAsync()
        );

    // a website that never chose a theme, or chose Paper, which the import treats as the default
    public bool IsOnPaper => ThemeInUse.Key == ThemePresets.Paper.Named.Key;
}
