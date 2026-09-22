using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Tests.Domain;

public class PostBodyTests
{
    [Fact]
    public void TheEmptyBodyIsADelta()
    {
        Assert.True(PostBody.IsDelta(PostBody.Empty.Json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""[{"insert":"Hello\n"}]""")]
    [InlineData("""{"text":"Hello"}""")]
    [InlineData("""{"ops":"Hello"}""")]
    public void RefusesWhatTheDatabaseWouldRefuse(string json)
    {
        Assert.False(PostBody.IsDelta(json));
    }
}
