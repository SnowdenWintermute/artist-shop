namespace ArtistShop.Web.Sites;

using System.Collections.Frozen;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;

// Every site's hosts, kept in memory so that finding a request's site costs no query. Loaded at
// startup, and again whenever the app changes a site's hosts. A site added by another process, such
// as tools/add-site, is only found after the app restarts
public sealed class SiteHostDirectory(SiteRepository siteRepository)
{
    // replaced whole rather than changed, so a request reading it sees the old hosts or the new,
    // never half of each. volatile, so every thread sees the replacement
    private volatile FrozenDictionary<HostName, SiteHost> _hosts = FrozenDictionary<HostName, SiteHost>.Empty;

    public async Task ReloadAsync()
    {
        var hosts = await siteRepository.GetHostsAsync();
        _hosts = hosts.ToFrozenDictionary(siteHost => siteHost.Host);
    }

    // null for a host no site has, and for anything that isn't a host name
    public SiteId? FindSite(string requestHost) =>
        HostName.Read(requestHost) is { } host && _hosts.TryGetValue(host, out var siteHost) ? siteHost.SiteId : null;
}
