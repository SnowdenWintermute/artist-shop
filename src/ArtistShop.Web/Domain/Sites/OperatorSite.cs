namespace ArtistShop.Web.Domain.Sites;

// A site with its owner's email, which is in Identity's database rather than the platform's. Null
// while the site has no owner
public record OperatorSite(SiteListing Site, EmailAddress? OwnerEmail);
