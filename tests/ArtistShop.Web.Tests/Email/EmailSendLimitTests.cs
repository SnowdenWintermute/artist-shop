using System.Net;
using System.Security.Claims;
using ArtistShop.Web.Email;
using Microsoft.AspNetCore.Http;

namespace ArtistShop.Web.Tests.Email;

// how the limits are set up and keyed; the rate limiting itself is .NET's
public sealed class EmailSendLimitTests
{
    [Fact]
    public void ASecondEmailToAnAddressIsRefusedWhoeverAsks()
    {
        using var limit = new RateLimitedEmailSendLimit();

        Assert.True(limit.TryTake("someone@example.com", "address:192.0.2.1"));
        Assert.False(limit.TryTake(" Someone@Example.com", "address:192.0.2.2"));
        Assert.True(limit.TryTake("someone-else@example.com", "address:192.0.2.1"));
    }

    // and the refused try doesn't count against the address
    [Fact]
    public void ARequesterIsRefusedAfterThirtyAddresses()
    {
        using var limit = new RateLimitedEmailSendLimit();

        for (var i = 0; i < 30; i++)
        {
            Assert.True(limit.TryTake($"{i}@example.com", "address:192.0.2.1"));
        }

        Assert.False(limit.TryTake("30@example.com", "address:192.0.2.1"));
        Assert.True(limit.TryTake("30@example.com", "address:192.0.2.2"));
    }

    [Fact]
    public void ASignedInRequesterIsTheirAccount()
    {
        var httpContext = Request("192.0.2.1");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "account-1")], "test"));

        Assert.Equal("account:account-1", EmailSendLimit.RequesterOf(httpContext));
    }

    [Theory]
    [InlineData("192.0.2.1", "address:192.0.2.1")]
    [InlineData("::ffff:192.0.2.1", "address:192.0.2.1")]
    [InlineData("2001:db8:1:2:3:4:5:6", "address:2001:db8:1:2::/64")]
    public void ASignedOutRequesterIsTheirAddress(string address, string requester) =>
        Assert.Equal(requester, EmailSendLimit.RequesterOf(Request(address)));

    private static DefaultHttpContext Request(string address) =>
        new() { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };
}
