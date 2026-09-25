using ArtistShop.Web.Domain.Platform;

namespace ArtistShop.Web.Tests.Domain.Platform;

public sealed class SignUpCodeTests
{
    [Fact]
    public void IsWrittenAsHexInGroupsOfFour()
    {
        Assert.Matches(@"\A[0-9A-F]{4}(-[0-9A-F]{4}){7}\z", SignUpCode.New().Text);
    }

    // people copy codes with stray spaces and type them in lowercase
    [Fact]
    public void ReadsBackTheSameCodeHoweverItsTyped()
    {
        var code = SignUpCode.New();

        foreach (var typed in new[] { code.Text, code.Text.ToLowerInvariant(), code.Text.Replace("-", " "), $" {code.Text.Replace("-", "")} " })
        {
            Assert.Equal(code.Hash(), SignUpCode.Read(typed)?.Hash());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("3F9A-0C21")]
    [InlineData("3F9A-0C21-77BE-0000-0000-0000-0000-000G")]
    [InlineData("3F9A-0C21-77BE-0000-0000-0000-0000-00000")]
    public void ReadsNothingThatIsntACode(string typed)
    {
        Assert.Null(SignUpCode.Read(typed));
    }

    [Fact]
    public void TwoNewCodesDiffer()
    {
        Assert.NotEqual(SignUpCode.New().Hash(), SignUpCode.New().Hash());
    }
}
