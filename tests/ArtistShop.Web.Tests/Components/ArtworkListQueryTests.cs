using ArtistShop.Web.Components.Catalog;

namespace ArtistShop.Web.Tests.Components;

public class ArtworkListQueryTests
{
    // what the filter form sends back with nothing chosen but images: every field, empty or not
    private const string SubmittedWithImagesOnly =
        "/admin/posts/pick-artwork?mode=change-image&q=&series=&images=yes&sale=&sort=added";

    [Theory]
    [InlineData("/admin/catalog/artworks", "/admin/catalog/artworks?q=&series=&images=&sale=&sort=added")]
    [InlineData("/admin/catalog/artworks", "/admin/catalog/artworks?sort=title&page=3")]
    [InlineData("/admin/posts/pick-artwork?images=yes", SubmittedWithImagesOnly)]
    [InlineData("/admin/posts/pick-artwork?images=yes&mode=change-image", SubmittedWithImagesOnly)]
    [InlineData("/admin/catalog/artworks?type=1&type=2", "/admin/catalog/artworks?type=2&type=1")]
    public void AddressesShowingTheSameArtworksHaveTheSameFilters(string cleared, string current)
    {
        Assert.True(ArtworkListQuery.ReadAddress(current).HasSameFiltersAs(ArtworkListQuery.ReadAddress(cleared)));
    }

    [Theory]
    [InlineData("/admin/catalog/artworks", "/admin/catalog/artworks?images=yes")]
    [InlineData("/admin/catalog/artworks", "/admin/catalog/artworks?q=rose")]
    [InlineData("/admin/posts/pick-artwork?images=yes", "/admin/posts/pick-artwork?q=&images=")]
    [InlineData("/admin/posts/pick-artwork?images=yes", "/admin/posts/pick-artwork?images=yes&series=4")]
    [InlineData("/admin/catalog/artworks?term=1", "/admin/catalog/artworks?term=1&term=2")]
    public void AddressesShowingOtherArtworksHaveDifferentFilters(string cleared, string current)
    {
        Assert.False(ArtworkListQuery.ReadAddress(current).HasSameFiltersAs(ArtworkListQuery.ReadAddress(cleared)));
    }
}
