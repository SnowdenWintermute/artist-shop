using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Domain;

// A file name read as another image of an artwork, as the bulk image upload does
public sealed class ArtworkNameTests
{
    [Theory]
    [InlineData("Dawn (2)", "Dawn", 2)]
    [InlineData("Dawn (0)", "Dawn", 0)]
    [InlineData("Dawn (-1)", "Dawn", -1)]
    [InlineData("Study (3) (12)", "Study (3)", 12)]
    public void ANumberInBracketsAfterTheTitleIsAnotherImage(string name, string artwork, int number)
    {
        Assert.Equal(new ExtraImageName(new ArtworkName(artwork), number), new ArtworkName(name).AsExtraImage());
    }

    // a title ending in a number is its own artwork's; the brackets and the space before them say it's another image
    [Theory]
    [InlineData("Study 2")]
    [InlineData("Dawn(2)")]
    [InlineData("Dawn (two)")]
    [InlineData("Dawn (2) detail")]
    [InlineData("(2)")]
    [InlineData("Dawn (99999999999)")]
    public void AnythingElseIsntAnotherImage(string name)
    {
        Assert.Null(new ArtworkName(name).AsExtraImage());
    }
}
