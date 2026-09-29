using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// website.json, as Download everything writes it and as it may come back
public sealed class WebsiteImportManifestTests
{
    private const string Valid = """
        {
          "formatVersion": 1,
          "listSeparator": "|",
          "artworkFiles": [ { "file": "artwork-type-3.csv", "artworkType": "Photo/Print" } ]
        }
        """;

    [Fact]
    public void ReadsTheSeparatorAndEachArtworkFilesType()
    {
        Assert.True(WebsiteImportManifest.TryRead(Valid, out var manifest, out _));

        Assert.Equal('|', manifest.ListSeparator);
        Assert.Equal([("artwork-type-3.csv", "Photo/Print")], manifest.ArtworkFiles);
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
    [InlineData("""{ "formatVersion": 1, "listSeparator": "||", "artworkFiles": [] }""")]
    [InlineData("""{ "formatVersion": 1, "listSeparator": "|" }""")]
    [InlineData("""{ "formatVersion": 1, "listSeparator": "|", "artworkFiles": [ { "file": "a.csv" } ] }""")]
    [InlineData("""{ "formatVersion": 1, "listSeparator": "|", "artworkFiles": [ { "file": 3, "artworkType": "Painting" } ] }""")]
    public void OneItCantReadIsUnreadable(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out var manifest, out var problem));
        Assert.Null(manifest);
        Assert.Equal("Its website.json isn't readable.", problem);
    }

    [Theory]
    [InlineData("""{ "formatVersion": 2, "listSeparator": "|", "artworkFiles": [] }""")]
    [InlineData("""{ "formatVersion": "1", "listSeparator": "|", "artworkFiles": [] }""")]
    [InlineData("""{ "listSeparator": "|", "artworkFiles": [] }""")]
    public void AnotherVersionIsRefused(string json)
    {
        Assert.False(WebsiteImportManifest.TryRead(json, out _, out var problem));
        Assert.Equal("Its website.json is from a version of Download everything this website doesn't read.", problem);
    }
}
