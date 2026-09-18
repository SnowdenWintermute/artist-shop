using ArtistShop.Web.Database;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

public sealed class DatabaseCollationComparerTests
{
    [Theory]
    // SQL Server ignores trailing spaces when it compares, so these are one name to the database
    [InlineData("Sunset", "Sunset ")]
    [InlineData("Sunset", "sunset")]
    [InlineData("Sunset", "SUNSET  ")]
    public void NamesTheDatabaseTreatsAsOneAreEqual(string left, string right)
    {
        Assert.True(DatabaseCollationComparer.Instance.Equals(left, right));
        Assert.Equal(
            DatabaseCollationComparer.Instance.GetHashCode(left),
            DatabaseCollationComparer.Instance.GetHashCode(right)
        );
    }

    [Theory]
    // a leading space is part of the name, and the collation is accent sensitive
    [InlineData("Sunset", " Sunset")]
    [InlineData("Café", "Cafe")]
    [InlineData("Sunset", "Sunrise")]
    public void NamesTheDatabaseTellsApartAreNotEqual(string left, string right)
    {
        Assert.False(DatabaseCollationComparer.Instance.Equals(left, right));
    }

    [Fact]
    public void ADecomposedFileNameMatchesAComposedArtworkNameOnceItIsANameAgain()
    {
        // what macOS hands over: "e" followed by U+0301, the combining acute accent
        var fromFinder = ArtworkName.FromFileName("Café.jpg");

        Assert.Equal("Café", fromFinder.Value);
    }
}
