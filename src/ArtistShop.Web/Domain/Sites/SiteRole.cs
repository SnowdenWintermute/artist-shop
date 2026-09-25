namespace ArtistShop.Web.Domain.Sites;

// what a member may do on a site; the numbers are site_members.role's
public enum SiteRole : byte
{
    // one per site: its admins' rights, and later deleting the site and choosing its admins
    Owner = 1,

    // edits the site's content
    Admin = 2,
}
