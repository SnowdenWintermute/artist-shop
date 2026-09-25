namespace ArtistShop.Web.Domain.Sites;

// userId is Identity's id for the member's account
public record SiteMember(string UserId, SiteRole Role);
