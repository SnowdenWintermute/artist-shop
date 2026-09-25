using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Components.Pages.Admin.Admins;

// a site member with their account's email, which Identity keeps and the platform doesn't
public record AdminListing(string UserId, string Email, SiteRole Role);
