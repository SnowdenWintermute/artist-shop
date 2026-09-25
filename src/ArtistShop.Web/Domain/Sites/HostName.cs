namespace ArtistShop.Web.Domain.Sites;

// A host a site is reached by, such as alice.artistshop.com. Always lowercase, since DNS names
// aren't case sensitive and a request's Host header may come in any case
public sealed record HostName
{
    public string Value { get; }

    private HostName(string value) => Value = value;

    // null for anything that isn't a DNS name, such as an IP address, an empty string or one with a
    // port. A trailing dot (the DNS root, "example.com.") names the same host, so it's dropped
    public static HostName? Read(string value)
    {
        var host = value.TrimEnd('.').ToLowerInvariant();

        return Uri.CheckHostName(host) is UriHostNameType.Dns ? new HostName(host) : null;
    }
}

public record SiteHost(HostName Host, SiteId SiteId, bool IsMain);
