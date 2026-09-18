using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class SeriesNameSimilarityTests
{
    [Theory]
    [InlineData("Seascapes, 1990s", "Seascapes 1990s")]
    [InlineData("Sea Scapes", "sea-scapes")]
    [InlineData("Winter", "winter")]
    public void NamesThatBuildOneAddressShareASlug(string name, string other)
    {
        Assert.True(SeriesNameSimilarity.ShareASlug(name, other));
    }

    [Fact]
    public void NamesWithDifferentLettersDoNotShareASlug()
    {
        Assert.False(SeriesNameSimilarity.ShareASlug("Seascapes", "Seascapes II"));
    }

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("Seascapes", "Seascapes", 0)]
    [InlineData("Seascapes", "Seacsapes", 2)]
    public void MeasuresEditDistance(string left, string right, int expected)
    {
        Assert.Equal(expected, SeriesNameSimilarity.EditDistance(left, right));
    }

    [Fact]
    public void ATransposedLetterIsANearDuplicate()
    {
        Assert.True(SeriesNameSimilarity.AreNearDuplicates("Seascapes", "Seacsapes"));
    }

    [Fact]
    public void ShortNamesAreNotComparedAtAll()
    {
        Assert.False(SeriesNameSimilarity.AreNearDuplicates("Blue", "Blur"));
    }

    [Fact]
    public void NamesThreeEditsApartAreNotNearDuplicates()
    {
        Assert.False(SeriesNameSimilarity.AreNearDuplicates("Seascapes", "Landscapes"));
    }

    [Fact]
    public void ChecksEachNewNameAgainstTheExistingSeries()
    {
        var checks = SeriesNameSimilarity.Check(["Seascapes 1990s", "Nightfall"], ["Seascapes, 1990s"]);

        var collision = Assert.Single(checks.SlugCollisions);
        Assert.Equal("Seascapes 1990s", collision.Name);
        Assert.Equal("Seascapes, 1990s", collision.MatchedName);
        Assert.Empty(checks.NearDuplicates);
    }

    [Fact]
    public void ChecksNewNamesAgainstEachOther()
    {
        var checks = SeriesNameSimilarity.Check(["Seascapes", "Seacsapes"], []);

        var nearDuplicate = Assert.Single(checks.NearDuplicates);
        Assert.Equal("Seacsapes", nearDuplicate.Name);
        Assert.Equal("Seascapes", nearDuplicate.MatchedName);
    }

    [Fact]
    public void APairThatSharesASlugIsNotAlsoANearDuplicate()
    {
        var checks = SeriesNameSimilarity.Check(["Seascapes 1990s"], ["Seascapes, 1990s"]);

        Assert.Single(checks.SlugCollisions);
        Assert.Empty(checks.NearDuplicates);
    }
}
