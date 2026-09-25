using ArtistShop.Web.Domain;

namespace ArtistShop.Web.Tests.Domain;

public class EmailAddressTests
{
    [Theory]
    [InlineData("alice@example.com", "alice@example.com")]
    [InlineData("  Alice@Example.COM ", "alice@example.com")]
    public void ReadsAnAddressTrimmedAndLowercase(string value, string expected) =>
        Assert.Equal(expected, EmailAddress.Read(value)?.Value);

    [Theory]
    [InlineData("")]
    [InlineData("alice")]
    [InlineData("@example.com")]
    [InlineData("alice@")]
    public void ReadsNothingElse(string value) => Assert.Null(EmailAddress.Read(value));

    // site_invites.email holds 254 characters
    [Fact]
    public void ReadsNothingTooLongToStore()
    {
        var domain = "@example.com";

        Assert.NotNull(EmailAddress.Read(new string('a', 254 - domain.Length) + domain));
        Assert.Null(EmailAddress.Read(new string('a', 255 - domain.Length) + domain));
    }
}
