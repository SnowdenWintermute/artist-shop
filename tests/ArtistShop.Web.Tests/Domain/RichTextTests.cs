using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class RichTextTests
{
    [Fact]
    public void TheEmptyBodyIsADelta()
    {
        Assert.True(RichText.IsDelta(RichText.Empty.Json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""[{"insert":"Hello\n"}]""")]
    [InlineData("""{"text":"Hello"}""")]
    [InlineData("""{"ops":"Hello"}""")]
    public void RefusesWhatTheDatabaseWouldRefuse(string json)
    {
        Assert.False(RichText.IsDelta(json));
    }
}
