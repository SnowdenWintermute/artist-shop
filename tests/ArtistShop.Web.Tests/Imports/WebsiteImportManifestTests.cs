using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// website.json, as Download everything writes it and as it may come back
public sealed class WebsiteImportManifestTests
{
    private const string Valid = """
        {
          "formatVersion": 3,
          "listSeparator": "|",
          "workFiles": [ { "file": "work-type-3.csv", "workType": "Photo/Print" } ]
        }
        """;

    [Fact]
    public void ReadsTheSeparatorAndEachWorkFilesType()
    {
        Assert.True(WebsiteImportManifest.TryRead(Valid, out var manifest, out _));

        Assert.Equal('|', manifest.ListSeparator);
        Assert.Equal([("work-type-3.csv", "Photo/Print")], manifest.WorkFiles);
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
    [InlineData("""{ "formatVersion": 3, "listSeparator": "||", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": 3, "listSeparator": "|" }""")]
    [InlineData("""{ "formatVersion": 3, "listSeparator": "|", "workFiles": [ { "file": "a.csv" } ] }""")]
    [InlineData("""{ "formatVersion": 3, "listSeparator": "|", "workFiles": [ { "file": 3, "workType": "Painting" } ] }""")]
    public void OneItCantReadIsUnreadable(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out var manifest, out var problem));
        Assert.Null(manifest);
        Assert.Equal("Its website.json isn't readable.", problem);
    }

    [Theory]
    // 2 had the wording, now in look.json
    [InlineData("""{ "formatVersion": 2, "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": 4, "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "formatVersion": "3", "listSeparator": "|", "workFiles": [] }""")]
    [InlineData("""{ "listSeparator": "|", "workFiles": [] }""")]
    public void AnotherVersionIsRefused(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out _, out var problem));
        Assert.Equal("Its website.json is from a version of Download everything this website doesn't read.", problem);
    }
}
