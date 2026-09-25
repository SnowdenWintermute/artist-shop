using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Tests.Sites;

public class SiteNameTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData(" Alice-Paints ", "alice-paints")]
    [InlineData("abc", "abc")]
    [InlineData("studio42", "studio42")]
    [InlineData("a-b-c", "a-b-c")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234", "abcdefghijklmnopqrstuvwxyz1234")]
    public void ReadsAnAllowedNameLowercase(string text, string expected) =>
        Assert.Equal(expected, SiteName.Read(text)?.Value);

    [Theory]
    [InlineData("ab", "3 to 30")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345", "3 to 30")]
    [InlineData("alice_paints", "letters a to z")]
    [InlineData("alice.paints", "letters a to z")]
    [InlineData("zoë", "letters a to z")]
    [InlineData("-alice", "start or end")]
    [InlineData("alice-", "start or end")]
    [InlineData("xn--bcher", "third and fourth")]
    [InlineData("www", "reserved")]
    [InlineData("Admin", "reserved")]
    public void RefusesANameTheRulesDont(string text, string problem)
    {
        Assert.Null(SiteName.Read(text));
        Assert.Contains(problem, SiteName.ProblemWith(text));
    }

    [Fact]
    public void ItsHostIsUnderThePlatforms()
    {
        var platformHost = HostName.Read("artshop.example.com") ?? throw new InvalidOperationException("Not a host.");
        var name = SiteName.Read("alice") ?? throw new InvalidOperationException("Not a name.");

        Assert.Equal("alice.artshop.example.com", name.HostUnder(platformHost).Value);
    }
}
