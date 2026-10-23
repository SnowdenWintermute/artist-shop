using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// look.json, as the look and wording download writes it and as it may come back
public sealed class LookImportFileTests
{
    private const string Collection = """{ "singular": "Project", "plural": "Projects", "keepsCase": false }""";

    private const string Evening = """{ "name": "Evening", "settings": { "colors": { "Page": "#202020", "Ink": "#ffffff", "Accent": "#ff0000" }, "fonts": {}, "frame": {} } }""";

    private const string Valid = """
        {
          "formatVersion": 1,
          "wording": {
            "collection": { "singular": "Project", "plural": "Projects", "keepsCase": false },
            "work": { "singular": null, "plural": null, "keepsCase": true },
            "description": { "label": "Notes", "keepsCase": true }
          },
          "themeInUse": { "theme": "evening" },
          "themes": [ { "name": "Evening", "settings": { "colors": { "Page": "#202020", "Ink": "#ffffff", "Accent": "#ff0000" }, "fonts": {}, "frame": {} } } ],
          "brandImages": [ { "type": "Logo", "file": "branding/logo.svg", "sha256": "ABababababababababababababababababababababababababababababababab" } ]
        }
        """;

    [Fact]
    public void ReadsTheWordingThemesAndBrandImages()
    {
        Assert.True(LookImportFile.TryRead(Valid, out var file, out _));

        Assert.Equal(
            new SiteWording(
                new NounChoice("Project", "Projects", KeepsCase: false),
                new NounChoice(null, null, KeepsCase: true),
                new LabelChoice("Notes", KeepsCase: true)
            ),
            file.Wording
        );
        // named as the theme itself is
        Assert.Equal(new ThemeInUseChoice.Saved("Evening"), file.ThemeInUse);
        var theme = Assert.Single(file.Themes);
        Assert.Equal("Evening", theme.Name);
        Assert.Equal(RgbColor.Parse("#202020"), theme.Theme.Colors.ChosenFor(ColorRole.Page));
        // a role with no font is Paper's
        Assert.Equal(ThemePresets.Paper.Theme.Fonts, theme.Theme.Fonts);
        Assert.Equal(ThemePresets.Paper.Theme.Frame, theme.Theme.Frame);
        Assert.Equal(
            [new LookImportBrandImage(BrandImageType.Logo, "branding/logo.svg", "abababababababababababababababababababababababababababababababab")],
            file.BrandImages
        );
    }

    [Fact]
    public void APresetIsReadByName()
    {
        Assert.True(LookImportFile.TryRead(Valid.Replace("""{ "theme": "evening" }""", """{ "preset": "Dark" }"""), out var file, out _));
        Assert.Equal(new ThemeInUseChoice.Preset(ThemePreset.Dark), file.ThemeInUse);
    }

    // as the Wording page saves a word, so a hand-edited file can't add spaces
    [Fact]
    public void WordsAreTrimmed()
    {
        var json = Valid.Replace(Collection, """{ "singular": " Project ", "plural": "Projects ", "keepsCase": false }""");

        Assert.True(LookImportFile.TryRead(json, out var file, out _));
        Assert.Equal(new NounChoice("Project", "Projects", KeepsCase: false), file.Wording.CollectionChoice);
    }

    // what the Wording and Theme pages couldn't have saved, or what the download couldn't have written
    [Theory]
    [InlineData(Collection, """{ "singular": "Project", "plural": null, "keepsCase": false }""")]
    [InlineData(Collection, """{ "singular": " ", "plural": "Projects", "keepsCase": false }""")]
    [InlineData(Collection, """{ "singular": "Project", "plural": "Projects" }""")]
    [InlineData(Collection, """{ "singular": "Project", "plural": "Projectsssssssssssssssssssssssssssssssssss", "keepsCase": false }""")]
    [InlineData("\"evening\"", "\"Morning\"")]
    [InlineData("\"#202020\"", "\"dark grey\"")]
    [InlineData("\"name\": \"Evening\"", "\"name\": \" \"")]
    [InlineData("\"Ink\": \"#ffffff\", ", "")]
    [InlineData(Evening, $"{Evening}, {Evening}")]
    [InlineData("\"sha256\": \"AB", "\"sha256\": \"")]
    public void WhatItCouldNotHaveSavedIsUnreadable(string from, string to)
    {
        Assert.Contains(from, Valid);
        Assert.False(LookImportFile.TryRead(Valid.Replace(from, to), out _, out var problem));
        Assert.Equal("Its look.json isn't readable.", problem);
    }

    // from a later version of the website, perhaps: the rest is still read
    [Theory]
    [InlineData("Mauve")]
    [InlineData("2")]
    public void APresetThisWebsiteDoesntHaveIsLeftOut(string preset)
    {
        var json = Valid.Replace("""{ "theme": "evening" }""", $$"""{ "preset": "{{preset}}" }""");

        Assert.True(LookImportFile.TryRead(json, out var file, out _));
        Assert.Equal(new ThemeInUseChoice.UnknownPreset(preset), file.ThemeInUse);
    }

    [Theory]
    [InlineData("Banner")]
    [InlineData("7")]
    public void ABrandImageTypeThisWebsiteDoesntHaveIsLeftOut(string type)
    {
        Assert.True(LookImportFile.TryRead(Valid.Replace("\"Logo\"", $"\"{type}\""), out var file, out _));
        Assert.Empty(file.BrandImages);
        Assert.Equal([type], file.UnknownBrandImageTypes);
    }

    [Fact]
    public void AFolderWithoutOneIsNotADownload()
    {
        Assert.False(LookImportFile.TryRead(null, out _, out var problem));
        Assert.Equal("The folder has no look.json. Choose the unzipped look and wording folder.", problem);
    }

    [Theory]
    [InlineData("\"formatVersion\": 2")]
    [InlineData("\"formatVersion\": \"1\"")]
    public void AnotherVersionIsRefused(string version)
    {
        Assert.False(LookImportFile.TryRead(Valid.Replace("\"formatVersion\": 1", version), out _, out var problem));
        Assert.Equal("Its look.json is from a version of the download this website doesn't read.", problem);
    }
}
