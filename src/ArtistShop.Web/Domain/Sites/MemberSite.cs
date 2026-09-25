namespace ArtistShop.Web.Domain.Sites;

// a site an account is a member of, as its "My websites" page lists it
public record MemberSite(SiteId SiteId, HostName MainHost, SiteRole Role);
