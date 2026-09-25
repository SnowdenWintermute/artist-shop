namespace ArtistShop.Web.Sites;

using System.Collections.Frozen;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;

// The platform's host and every site's, kept in memory so that finding a request's host costs no
// query. Loaded at startup, and again whenever the app changes a site's hosts. A site added by
// another process, such as tools/add-site, is only found after the app restarts
public sealed class HostDirectory(SiteRepository siteRepository, PlatformSettings platformSettings)
{
    // replaced whole rather than changed, so a request reading it sees the old hosts or the new,
    // never half of each. volatile, so every thread sees the replacement
    private volatile FrozenDictionary<HostName, SiteHost> _siteHosts = FrozenDictionary<HostName, SiteHost>.Empty;

    public async Task ReloadAsync()
    {
        var siteHosts = await siteRepository.GetHostsAsync();

        // the platform would win the lookup, so the site would never be reached
        if (siteHosts.Find(siteHost => siteHost.Host == platformSettings.Host) is { } clash)
        {
            throw new InvalidOperationException(
                $"Site {clash.SiteId.Value} has the platform's host, {platformSettings.Host.Value}."
            );
        }

        _siteHosts = siteHosts.ToFrozenDictionary(siteHost => siteHost.Host);
    }

    // null for a host neither the platform nor any site has, and for anything that isn't a host name
    public CurrentHost? Find(string requestHost)
    {
        if (HostName.Read(requestHost) is not { } host)
        {
            return null;
        }

        if (host == platformSettings.Host)
        {
            return new CurrentHost.Platform();
        }

        return _siteHosts.TryGetValue(host, out var siteHost) ? new CurrentHost.Site(siteHost.SiteId) : null;
    }
}
