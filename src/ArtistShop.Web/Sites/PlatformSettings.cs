namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Sites;

// The platform's own host, from Platform:Host, and its name, from Platform:Name. The websites don't
// show the name, except where an account is made or deleted: an account is the platform's, for
// every website on it
public sealed record PlatformSettings(HostName Host, string Name);
