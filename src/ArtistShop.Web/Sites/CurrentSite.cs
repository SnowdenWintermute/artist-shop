namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Domain.Sites;

// The site a request is for, as a scoped service
public sealed record CurrentSite(SiteId Id);

// Until the platform database lists the sites and a request's host picks one (multi-tenancy step 3
// in todo.md), there is exactly one site and every request is for it
public static class SingleSite
{
    public static readonly SiteId Id = new(1);
}
