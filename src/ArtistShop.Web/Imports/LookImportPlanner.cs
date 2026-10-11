using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Imports;

// The unzipped look and wording folder as the page read it. Json is null when there's no look.json;
// FileIdsByPath is each file beside it by its path there, as the id the page's script knows it by
public record LookImportFolder(string? Json, IReadOnlyDictionary<string, string> FileIdsByPath)
{
    public static readonly LookImportFolder Empty = new(null, new Dictionary<string, string>());
}

public enum LookImportOutcome
{
    // the download's is already this website's
    Same,

    // this website is still on the default, so it takes the download's
    Set,

    // this website chose its own, which the import leaves alone, as it only adds elsewhere
    Kept,
}

public enum ThemeImportOutcome
{
    Add,

    // a theme of this name with the same colours and fonts is here already
    Same,

    // a different theme here has this name, so the download's is left out
    NameTaken,
}

public record ThemeImportItem(LookImportTheme Theme, ThemeImportOutcome Outcome);

// NameTaken: this website is on Paper, but the download's theme in use was left out for its name.
// UnknownPreset: the download's is a preset this website doesn't have
public enum ThemeInUseImportOutcome
{
    Same,
    Set,
    Kept,
    NameTaken,
    UnknownPreset,
}

public record ThemeInUseImportPlan(ThemeInUseChoice Choice, ThemeInUseImportOutcome Outcome)
{
    public string Name =>
        Choice switch
        {
            ThemeInUseChoice.Preset preset => ThemePresets.For(preset.Value).Name,
            ThemeInUseChoice.Saved saved => saved.Name,
            ThemeInUseChoice.UnknownPreset unknown => unknown.Name,
            _ => throw new InvalidOperationException($"{Choice.GetType().Name} isn't a theme choice."),
        };
}

// FileId is null when the file isn't in the folder, which leaves the image out
public record BrandImageImportItem(LookImportBrandImage Image, string? FileId, LookImportOutcome Outcome)
{
    public bool WillUpload => FileId is not null && Outcome is LookImportOutcome.Set;
}

// UnknownBrandImageTypes are left out, as in LookImportFile
public record LookImportPlan(
    SiteWording Wording,
    LookImportOutcome WordingOutcome,
    IReadOnlyList<ThemeImportItem> Themes,
    ThemeInUseImportPlan ThemeInUse,
    IReadOnlyList<BrandImageImportItem> BrandImages,
    IReadOnlyList<string> UnknownBrandImageTypes
)
{
    // for a folder that can't be imported, whose review shows nothing else
    public static readonly LookImportPlan Empty = new(
        SiteWording.Default,
        LookImportOutcome.Same,
        [],
        new ThemeInUseImportPlan(new ThemeInUseChoice.Preset(ThemePresets.Paper.Preset), ThemeInUseImportOutcome.Same),
        [],
        []
    );
}

// What the import did on the server, and the brand images left for the page's script to upload
public record LookImportResult(bool WordingSet, int ThemesAdded, bool ThemeInUseSet, IReadOnlyList<BrandImageImportItem> BrandImagesToUpload);

public static class LookImportPlanner
{
    public static LookImportPlan Plan(LookImportFile file, LookImportFolder folder, SiteLook target)
    {
        var themes = file.Themes.Select(theme => new ThemeImportItem(theme, ThemeOutcome(theme, target.Themes))).ToList();

        return new LookImportPlan(
            file.Wording,
            file.Wording == target.Wording ? LookImportOutcome.Same
            : target.Wording == SiteWording.Default ? LookImportOutcome.Set
            : LookImportOutcome.Kept,
            themes,
            new ThemeInUseImportPlan(file.ThemeInUse, ThemeInUseOutcome(file.ThemeInUse, themes, target)),
            [
                .. file.BrandImages.Select(image => new BrandImageImportItem(
                    image,
                    folder.FileIdsByPath.GetValueOrDefault(image.File),
                    target.BrandImages.FirstOrDefault(original => original.Type == image.Type) is not { } original
                        ? LookImportOutcome.Set
                    : original.Sha256 == image.Sha256 ? LookImportOutcome.Same
                    : LookImportOutcome.Kept
                )),
            ],
            file.UnknownBrandImageTypes
        );
    }

    // Planned again against the website as it is now, like each catalog stage. The brand images
    // are left for the page's script, which sends each to the Branding page's upload endpoint
    public static async Task<LookImportResult> ImportAsync(
        LookImportFile file,
        LookImportFolder folder,
        WordingRepository wordingRepository,
        ThemeRepository themeRepository,
        BrandImageStore brandImageStore
    )
    {
        var plan = Plan(file, folder, await SiteLook.LoadAsync(wordingRepository, themeRepository, brandImageStore));

        var wordingSet = plan.WordingOutcome is LookImportOutcome.Set;

        if (wordingSet)
        {
            await wordingRepository.UpdateAsync(plan.Wording);
        }

        var themesAdded = 0;

        foreach (var item in plan.Themes.Where(item => item.Outcome is ThemeImportOutcome.Add))
        {
            try
            {
                await themeRepository.AddAsync(item.Theme.Name, item.Theme.Theme);
                themesAdded++;
            }
            // added since the plan, so left alone as a taken name is
            catch (NameAlreadyInUseException)
            {
            }
        }

        var themeInUseSet = plan.ThemeInUse.Outcome is ThemeInUseImportOutcome.Set;

        if (themeInUseSet)
        {
            await themeRepository.UseAsync(await KeyOfAsync(plan.ThemeInUse.Choice, themeRepository));
        }

        return new LookImportResult(wordingSet, themesAdded, themeInUseSet, [.. plan.BrandImages.Where(image => image.WillUpload)]);
    }

    private static async Task<ThemeKey> KeyOfAsync(ThemeInUseChoice choice, ThemeRepository themeRepository) =>
        choice switch
        {
            ThemeInUseChoice.Preset preset => new ThemeKey.Preset(preset.Value),
            ThemeInUseChoice.Saved saved => new ThemeKey.Saved(
                (await themeRepository.GetAllAsync()).Single(theme => ImportNames.Comparer.Equals(theme.Name, saved.Name)).Id
            ),
            _ => throw new InvalidOperationException($"{choice.GetType().Name} isn't a theme choice."),
        };

    // compared as the database keeps them, so only what the theme chose counts
    private static ThemeImportOutcome ThemeOutcome(LookImportTheme theme, IReadOnlyList<SavedTheme> existing) =>
        existing.FirstOrDefault(other => ImportNames.Comparer.Equals(other.Name, theme.Name)) is not { } named ? ThemeImportOutcome.Add
        : ThemeSettingsJson.Write(named.Theme) == ThemeSettingsJson.Write(theme.Theme) ? ThemeImportOutcome.Same
        : ThemeImportOutcome.NameTaken;

    // A website on Paper takes the download's theme, unless it's a saved one left out for its name
    private static ThemeInUseImportOutcome ThemeInUseOutcome(
        ThemeInUseChoice choice,
        IReadOnlyList<ThemeImportItem> themes,
        SiteLook target
    )
    {
        switch (choice)
        {
            case ThemeInUseChoice.Preset preset:
                return target.ThemeInUse.Key == new ThemeKey.Preset(preset.Value) ? ThemeInUseImportOutcome.Same
                    : target.IsOnPaper ? ThemeInUseImportOutcome.Set
                    : ThemeInUseImportOutcome.Kept;

            case ThemeInUseChoice.Saved saved:
                var item = themes.Single(theme => ImportNames.Comparer.Equals(theme.Theme.Name, saved.Name));

                return item.Outcome is ThemeImportOutcome.Same
                    && target.ThemeInUse.IsSaved
                    && ImportNames.Comparer.Equals(target.ThemeInUse.Name, saved.Name)
                        ? ThemeInUseImportOutcome.Same
                    : !target.IsOnPaper ? ThemeInUseImportOutcome.Kept
                    : item.Outcome is ThemeImportOutcome.NameTaken ? ThemeInUseImportOutcome.NameTaken
                    : ThemeInUseImportOutcome.Set;

            case ThemeInUseChoice.UnknownPreset:
                return ThemeInUseImportOutcome.UnknownPreset;

            default:
                throw new InvalidOperationException($"{choice.GetType().Name} isn't a theme choice.");
        }
    }
}
