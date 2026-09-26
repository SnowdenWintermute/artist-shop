namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Sites;

// The site a request or a circuit is for, as a scoped service. Only a site's pages and endpoints ask
// for it, and ServedOn keeps those off the platform's host
public sealed record CurrentSite(SiteId Id, HostName MainHost)
{
    public static CurrentSite From(IServiceProvider services) =>
        services.GetRequiredService<CurrentHost>() is CurrentHost.Site site
            ? new CurrentSite(site.Id, site.MainHost)
            : throw new InvalidOperationException("A site's services were asked for on the platform's host.");
}
