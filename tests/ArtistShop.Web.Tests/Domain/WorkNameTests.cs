using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Domain;

// A file name read as another image of a work, as the bulk image upload does
public sealed class WorkNameTests
{
    [Theory]
    [InlineData("Dawn (2)", "Dawn", 2)]
    [InlineData("Dawn (0)", "Dawn", 0)]
    [InlineData("Dawn (-1)", "Dawn", -1)]
    [InlineData("Study (3) (12)", "Study (3)", 12)]
    public void ANumberInBracketsAfterTheTitleIsAnotherImage(string name, string work, int number)
    {
        Assert.Equal(new ExtraImageName(new WorkName(work), number), new WorkName(name).AsExtraImage());
    }

    // a title ending in a number is its own work's; the brackets and the space before them say it's another image
    [Theory]
    [InlineData("Study 2")]
    [InlineData("Dawn(2)")]
    [InlineData("Dawn (two)")]
    [InlineData("Dawn (2) detail")]
    [InlineData("(2)")]
    [InlineData("Dawn (99999999999)")]
    public void AnythingElseIsntAnotherImage(string name)
    {
        Assert.Null(new WorkName(name).AsExtraImage());
    }
}
