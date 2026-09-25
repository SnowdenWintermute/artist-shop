using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Tests.Sites;

public class HostNameTests
{
    [Theory]
    [InlineData("alice.artistshop.com", "alice.artistshop.com")]
    [InlineData("Shop.BobArt.com", "shop.bobart.com")]
    [InlineData("alicepaints.com.", "alicepaints.com")]
    [InlineData("localhost", "localhost")]
    public void ReadsAHostLowercase(string value, string expected) => Assert.Equal(expected, HostName.Read(value)?.Value);

    [Theory]
    [InlineData("")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    [InlineData("localhost:5000")]
    [InlineData("alice paints.com")]
    [InlineData("alice/../bob.com")]
    public void ReadsNothingElse(string value) => Assert.Null(HostName.Read(value));
}
