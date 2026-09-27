using System.Net;
using System.Security.Claims;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Http;

namespace ArtistShop.Web.Tests.Utilities;

public sealed class RequesterTests
{
    [Fact]
    public void ASignedInRequesterIsTheirAccountAndAddress()
    {
        var httpContext = Request("192.0.2.1");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "account-1")], "test"));

        Assert.Equal(new Requester("192.0.2.1", "account-1"), Requester.Of(httpContext));
    }

    [Theory]
    [InlineData("192.0.2.1", "192.0.2.1")]
    [InlineData("::ffff:192.0.2.1", "192.0.2.1")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    public void ASignedOutRequesterIsTheirAddress(string address, string key) =>
        Assert.Equal(new Requester(key, null), Requester.Of(Request(address)));

    private static DefaultHttpContext Request(string address) =>
        new() { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };
}
