namespace ArtistShop.Web.Domain.Sites;

// One artist's website: its portfolio, blog and, later, its shop. Permanent, unlike the host names
// it's reached by, so anything kept per site (such as its image folder) is named by this
public record SiteId(int Value);
