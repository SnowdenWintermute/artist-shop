using System.Collections.Concurrent;
using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Exports;

// One image or post download per site at a time: each reads originals from disk, gigabytes of
// them for images, so a website can't start many at once and crowd out every other website's requests
public sealed class ExportLock
{
    private readonly ConcurrentDictionary<SiteId, byte> _running = new();

    // null while another of the site's downloads runs; disposing ends this one
    public IDisposable? TryAcquire(SiteId siteId) =>
        _running.TryAdd(siteId, 0) ? new Release(_running, siteId) : null;

    private sealed class Release(ConcurrentDictionary<SiteId, byte> running, SiteId siteId) : IDisposable
    {
        public void Dispose() => running.TryRemove(siteId, out _);
    }
}
