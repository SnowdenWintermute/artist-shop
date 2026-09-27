namespace ArtistShop.Web.Utilities;

using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

// Who made a request, for the rate limits to count: the address it came from, and the account when
// signed in. Behind nginx the address is the visitor's only with ASPNETCORE_FORWARDEDHEADERS_ENABLED
// (docker-compose.production.yml); without it every visitor is nginx's address
public sealed record Requester(string Address, string? AccountId)
{
    public static Requester Of(HttpContext httpContext) =>
        new(AddressKey(httpContext.Connection.RemoteIpAddress), httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));

    private static string AddressKey(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        // a home or a server is given a whole /64 and can pick any address in it, so only the first
        // half says who it is
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
