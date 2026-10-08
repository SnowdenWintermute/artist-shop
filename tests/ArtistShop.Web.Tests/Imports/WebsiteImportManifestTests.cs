using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// website.json, as Download everything writes it and as it may come back
public sealed class WebsiteImportManifestTests
{
    private const string Valid = """
        {
          "formatVersion": 2,
          "listSeparator": "|",
          "workFiles": [ { "file": "work-type-3.csv", "workType": "Photo/Print" } ],
          "wording": {
            "collection": { "singular": "Project", "plural": "Projects", "keepsCase": false },
            "work": { "singular": null, "plural": null, "keepsCase": true }
          }
        }
        """;

    private const string DefaultWording = """
        "wording": {
          "collection": { "singular": null, "plural": null, "keepsCase": false },
          "work": { "singular": null, "plural": null, "keepsCase": false }
        }
        """;

    [Fact]
    public void ReadsTheSeparatorAndEachWorkFilesType()
    {
        Assert.True(WebsiteImportManifest.TryRead(Valid, out var manifest, out _));

        Assert.Equal('|', manifest.ListSeparator);
        Assert.Equal([("work-type-3.csv", "Photo/Print")], manifest.WorkFiles);
        Assert.Equal(
            new SiteWording(new NounChoice("Project", "Projects", KeepsCase: false), new NounChoice(null, null, KeepsCase: true)),
            manifest.Wording
        );
    }

    // as the Wording page saves a word, so a hand-edited file can't add spaces
    [Fact]
    public void WordsAreTrimmed()
    {
        var json = Valid.Replace("\"Project\", \"plural\": \"Projects\"", "\" Project \", \"plural\": \"Projects \"");

        Assert.True(WebsiteImportManifest.TryRead(json, out var manifest, out _));
        Assert.Equal(new NounChoice("Project", "Projects", KeepsCase: false), manifest.Wording.CollectionChoice);
    }

    // words the Wording page couldn't have saved
    [Theory]
    [InlineData("""{ "singular": "Project", "plural": null, "keepsCase": false }""")]
    [InlineData("""{ "singular": " ", "plural": "Projects", "keepsCase": false }""")]
    [InlineData("""{ "singular": "Project", "plural": "Projects" }""")]
    [InlineData("""{ "singular": "Project", "plural": "Projectsssssssssssssssssssssssssssssssssss", "keepsCase": false }""")]
    public void WordingItCouldNotHaveSavedIsUnreadable(string collection)
    {
        var json = Valid.Replace("""{ "singular": "Project", "plural": "Projects", "keepsCase": false }""", collection);

        Assert.False(WebsiteImportManifest.TryRead(json, out _, out var problem));
        Assert.Equal("Its website.json isn't readable.", problem);
    }

    [Fact]
    public void WithoutWordingItIsUnreadable()
    {
        Assert.False(WebsiteImportManifest.TryRead("""{ "formatVersion": 2, "listSeparator": "|", "workFiles": [] }""", out _, out _));
        Assert.True(WebsiteImportManifest.TryRead($$"""{ "formatVersion": 2, "listSeparator": "|", "workFiles": [], {{DefaultWording}} }""", out var manifest, out _));
        Assert.Equal(SiteWording.Default, manifest.Wording);
    }

    [Fact]
    public void AFolderWithoutOneIsNotADownload()
    {
        Assert.False(WebsiteImportManifest.TryRead(null, out _, out var problem));
        Assert.Equal("The folder has no website.json. Choose the unzipped folder from Download everything.", problem);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "formatVersion": 2, "listSeparator": "||", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": 2, "listSeparator": "|" }""")]
    [InlineData("""{ "formatVersion": 2, "listSeparator": "|", "workFiles": [ { "file": "a.csv" } ] }""")]
    [InlineData("""{ "formatVersion": 2, "listSeparator": "|", "workFiles": [ { "file": 3, "workType": "Painting" } ] }""")]
    public void OneItCantReadIsUnreadable(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out var manifest, out var problem));
        Assert.Null(manifest);
        Assert.Equal("Its website.json isn't readable.", problem);
    }

    [Theory]
    // 1 is from before the wording and the rename to works
    [InlineData("""{ "formatVersion": 1, "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": 3, "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": "2", "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "listSeparator": "|", "workFiles": [] }""")]
    public void AnotherVersionIsRefused(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out _, out var problem));
        Assert.Equal("Its website.json is from a version of Download everything this website doesn't read.", problem);
    }
}
