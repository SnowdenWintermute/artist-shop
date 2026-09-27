namespace ArtistShop.Web.Email;

using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

// Whether an email someone asked for may go. Register, Forgot password, the account page's Password
// box and admin invitations each email an address typed in, so without a limit anyone could flood an
// inbox and spend the sending reputation SES judges the domain by. Emails that follow something done,
// such as a deletion or a handover, aren't limited. RateLimitedEmailSendLimit in the app; the tests'
// fake refuses the addresses it's told to
public abstract class EmailSendLimit
{
    // false when the email mustn't go; true counts it as sent
    public abstract bool TryTake(string recipient, string requester);

    // the signed-in account, or else the address the request came from
    public static string RequesterOf(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } accountId
            ? $"account:{accountId}"
            : $"address:{AddressKey(httpContext.Connection.RemoteIpAddress)}";

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
