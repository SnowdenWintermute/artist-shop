namespace ArtistShop.Web.Domain.Sites;

// a site member with their account's email, which Identity keeps and the platform doesn't
public record SiteMemberAccount(string UserId, EmailAddress Email, SiteRole Role);
