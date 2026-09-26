namespace ArtistShop.Web.Domain.Sites;

// A site an account is a member of, as its "My websites" page lists it. EraseAt is when it's erased,
// if its owner deleted it
public record MemberSite(SiteId SiteId, HostName MainHost, SiteRole Role, DateTimeOffset? EraseAt)
{
    public bool IsBeingDeleted => EraseAt is not null;

    // keep_site's rule: once its erase date has come, SiteEraser may be partway through it
    public bool CanStillBeKept(DateTimeOffset now) => EraseAt > now;
}
