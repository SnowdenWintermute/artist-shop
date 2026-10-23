using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// what importing a look and wording folder would do to a website, given what it has
public sealed class LookImportPlannerTests
{
    private static readonly Theme Evening = Theme("#202020");

    private static readonly SiteLook Fresh = new(SiteWording.Default, ThemePresets.Paper.Named, [], []);

    [Fact]
    public void AFreshWebsiteTakesEverything()
    {
        var file = File(new ThemeInUseChoice.Saved("Evening"), new LookImportTheme("Evening", Evening));

        var plan = LookImportPlanner.Plan(file, Folder, Fresh);

        Assert.Equal(LookImportOutcome.Set, plan.WordingOutcome);
        Assert.Equal(ThemeImportOutcome.Add, Assert.Single(plan.Themes).Outcome);
        Assert.Equal(ThemeInUseImportOutcome.Set, plan.ThemeInUse.Outcome);
        var logo = Assert.Single(plan.BrandImages);
        Assert.Equal((LookImportOutcome.Set, "logo-id"), (logo.Outcome, logo.FileId));
        Assert.True(logo.WillUpload);
    }

    [Fact]
    public void TheWebsiteItCameFromHasItAllAlready()
    {
        var file = File(new ThemeInUseChoice.Saved("Evening"), new LookImportTheme("Evening", Evening));
        var saved = new SavedTheme(new ThemeId(4), "evening", Evening);
        var target = new SiteLook(Wording, saved.Named, [saved], [Logo(LogoSha256)]);

        var plan = LookImportPlanner.Plan(file, Folder, target);

        Assert.Equal(LookImportOutcome.Same, plan.WordingOutcome);
        Assert.Equal(ThemeImportOutcome.Same, Assert.Single(plan.Themes).Outcome);
        Assert.Equal(ThemeInUseImportOutcome.Same, plan.ThemeInUse.Outcome);
        Assert.Equal(LookImportOutcome.Same, Assert.Single(plan.BrandImages).Outcome);
    }

    // the import only adds, so what a website chose itself stays
    [Fact]
    public void AWebsitesOwnChoicesAreKept()
    {
        var file = File(new ThemeInUseChoice.Preset(ThemePreset.Dark));
        var own = new SiteWording(new NounChoice("Body of work", "Bodies of work", KeepsCase: false), NounChoice.Default, LabelChoice.Default);
        var saved = new SavedTheme(new ThemeId(4), "Mine", Evening);
        var target = new SiteLook(own, saved.Named, [saved], [Logo(new('c', 64))]);

        var plan = LookImportPlanner.Plan(file, Folder, target);

        Assert.Equal(LookImportOutcome.Kept, plan.WordingOutcome);
        Assert.Equal(ThemeInUseImportOutcome.Kept, plan.ThemeInUse.Outcome);
        Assert.Equal(LookImportOutcome.Kept, Assert.Single(plan.BrandImages).Outcome);
        Assert.False(plan.BrandImages[0].WillUpload);
    }

    // a different theme of the same name is left as it is, and so isn't used either
    [Fact]
    public void ATakenNameLeavesTheThemeOut()
    {
        var file = File(new ThemeInUseChoice.Saved("Evening"), new LookImportTheme("Evening", Evening));
        var target = Fresh with { Themes = [new SavedTheme(new ThemeId(4), "EVENING", Theme("#303030"))] };

        var plan = LookImportPlanner.Plan(file, Folder, target);

        Assert.Equal(ThemeImportOutcome.NameTaken, Assert.Single(plan.Themes).Outcome);
        Assert.Equal(ThemeInUseImportOutcome.NameTaken, plan.ThemeInUse.Outcome);
    }

    [Fact]
    public void AnImageMissingFromTheFolderIsntUploaded()
    {
        var plan = LookImportPlanner.Plan(File(new ThemeInUseChoice.Preset(ThemePreset.Paper)), LookImportFolder.Empty, Fresh);

        var logo = Assert.Single(plan.BrandImages);
        Assert.Null(logo.FileId);
        Assert.False(logo.WillUpload);
        Assert.Equal(ThemeInUseImportOutcome.Same, plan.ThemeInUse.Outcome);
    }

    // a preset this website doesn't have, and a type of brand image it doesn't have, are left out
    [Fact]
    public void WhatThisWebsiteDoesntHaveIsLeftOut()
    {
        var file = File(new ThemeInUseChoice.UnknownPreset("Mauve")) with { UnknownBrandImageTypes = ["Banner"] };

        var plan = LookImportPlanner.Plan(file, Folder, Fresh);

        Assert.Equal(ThemeInUseImportOutcome.UnknownPreset, plan.ThemeInUse.Outcome);
        Assert.Equal("Mauve", plan.ThemeInUse.Name);
        Assert.Equal(["Banner"], plan.UnknownBrandImageTypes);
        Assert.True(Assert.Single(plan.BrandImages).WillUpload);
    }

    private static readonly SiteWording Wording = new(new NounChoice("Project", "Projects", KeepsCase: false), NounChoice.Default, LabelChoice.Default);

    private static readonly string LogoSha256 = new('a', 64);

    private static readonly LookImportFolder Folder = new(null, new Dictionary<string, string> { ["branding/logo.png"] = "logo-id" });

    private static LookImportFile File(ThemeInUseChoice themeInUse, params LookImportTheme[] themes) =>
        new(Wording, themeInUse, themes, [new LookImportBrandImage(BrandImageType.Logo, "branding/logo.png", LogoSha256)], []);

    private static BrandImageOriginal Logo(string sha256) => new(BrandImageType.Logo, "logo-key", "logo.png", sha256);

    private static Theme Theme(string page) =>
        new(
            new ThemeColors(
                new Dictionary<ColorRole, RgbColor>
                {
                    [ColorRole.Page] = RgbColor.Parse(page),
                    [ColorRole.Ink] = RgbColor.White,
                    [ColorRole.Accent] = RgbColor.Parse("#ff0000"),
                }
            ),
            ThemePresets.Paper.Theme.Fonts,
            ThemePresets.Paper.Theme.Frame
        );
}
